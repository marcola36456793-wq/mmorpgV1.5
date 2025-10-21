using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
/// ✅ CORRIGIDO - Gerenciador de skills no cliente Unity
/// Valida range antes de usar skills
/// Move até o range e então usa
/// </summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [Header("UI")]
    public Transform skillHotbarContainer;
    public GameObject skillSlotPrefab;

    [Header("Visual Effects")]
    public GameObject defaultSkillEffectPrefab;
    
    private List<SkillSlotUI> skillSlots = new List<SkillSlotUI>();
    private Dictionary<int, LearnedSkillData> learnedSkills = new Dictionary<int, LearnedSkillData>();
    
    private int currentTargetMonsterId = -1;

    // ✅ NOVO - Controle de movimento para skill
    private bool movingToUseSkill = false;
    private int pendingSkillId = 0;
    private int pendingSlotNumber = 0;
    private Vector3 targetPositionForSkill;
    private float skillRange = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CreateSkillSlots();
        RegisterMessageHandlers();
    }

    private void Update()
    {
        // ✅ NOVO - Verifica se chegou no range para usar skill
        if (movingToUseSkill)
        {
            CheckSkillRangeAndUse();
        }
    }

    private void CreateSkillSlots()
    {
        if (skillSlotPrefab == null || skillHotbarContainer == null)
        {
            Debug.LogError("SkillManager: Missing prefab or container!");
            return;
        }

        for (int i = 1; i <= 9; i++)
        {
            GameObject slotObj = Instantiate(skillSlotPrefab, skillHotbarContainer);
            SkillSlotUI slot = slotObj.GetComponent<SkillSlotUI>();
            
            if (slot != null)
            {
                slot.slotNumber = i;
                skillSlots.Add(slot);
            }
        }

        Debug.Log($"✅ Created {skillSlots.Count} skill slots");
    }

    private void RegisterMessageHandlers()
    {
        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnSelectCharacterResponse += HandleCharacterSelected;
        }
    }

    private void HandleCharacterSelected(SelectCharacterResponseData data)
    {
        if (data.success && data.character != null)
        {
            RequestSkills();
        }
    }

    public void RequestSkills()
    {
        var message = new
        {
            type = "getSkills"
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    public void UpdateSkills(List<LearnedSkillData> skills)
    {
        learnedSkills.Clear();

        foreach (var skill in skills)
        {
            learnedSkills[skill.skillId] = skill;
        }

        RefreshHotbar();
        
        Debug.Log($"📚 Loaded {learnedSkills.Count} skills");
    }

    private void RefreshHotbar()
    {
        foreach (var slot in skillSlots)
        {
            slot.Clear();
        }

        foreach (var kvp in learnedSkills)
        {
            var skill = kvp.Value;
            
            if (skill.slotNumber >= 1 && skill.slotNumber <= 9)
            {
                var slot = skillSlots.FirstOrDefault(s => s.slotNumber == skill.slotNumber);
                
                if (slot != null)
                {
                    slot.SetSkill(skill);
                }
            }
        }
    }

    public void SetCurrentTarget(int monsterId)
    {
        currentTargetMonsterId = monsterId;
        Debug.Log($"🎯 SkillManager: Target set: Monster ID {monsterId}");
    }

    public void ClearCurrentTarget()
    {
        currentTargetMonsterId = -1;
        movingToUseSkill = false;
        pendingSkillId = 0;
        Debug.Log($"🎯 SkillManager: Target cleared");
    }

    /// <summary>
    /// ✅ CORRIGIDO - Usa skill com validação de range
    /// Se não estiver no range, move até lá primeiro
    /// </summary>
    public void UseSkill(int skillId, int slotNumber)
    {
        if (!learnedSkills.TryGetValue(skillId, out var skill))
        {
            Debug.LogWarning($"❌ Skill {skillId} not learned!");
            return;
        }

        if (skill.template == null)
        {
            Debug.LogWarning($"❌ Skill {skillId} has no template!");
            return;
        }

        // ✅ VALIDAÇÃO: Verifica se precisa de target
        if (skill.template.targetType == "enemy")
        {
            // Verifica se tem target selecionado
            if (currentTargetMonsterId <= 0)
            {
                Debug.Log("❌ Nenhum alvo selecionado!");
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>❌ Selecione um alvo primeiro!</color>");
                }
                return;
            }

            // Busca o monstro
            var monsterObj = GameObject.Find($"Monster_{currentTargetMonsterId}") ?? 
                            FindMonsterByIdInScene(currentTargetMonsterId);
            
            if (monsterObj == null)
            {
                Debug.LogWarning($"❌ Monster {currentTargetMonsterId} not found in scene!");
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>❌ Alvo não encontrado!</color>");
                }
                return;
            }

            var monsterController = monsterObj.GetComponent<MonsterController>();
            
            if (monsterController == null || !monsterController.isAlive)
            {
                Debug.LogWarning($"❌ Monster {currentTargetMonsterId} is dead or invalid!");
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>❌ Alvo inválido!</color>");
                }
                return;
            }

            // ✅ VALIDAÇÃO DE RANGE
            var player = GameObject.FindGameObjectWithTag("Player");
            
            if (player == null)
            {
                Debug.LogError("❌ Local player not found!");
                return;
            }

            float distance = Vector3.Distance(player.transform.position, monsterObj.transform.position);
            float range = skill.template.range;

            Debug.Log($"📏 Distance to target: {distance:F2}m, Skill range: {range:F2}m");

            if (distance > range)
            {
                // ✅ NÃO ESTÁ NO RANGE - MOVE ATÉ LÁ
                Debug.Log($"🏃 Too far! Moving to range first...");
                
                movingToUseSkill = true;
                pendingSkillId = skillId;
                pendingSlotNumber = slotNumber;
                targetPositionForSkill = monsterObj.transform.position;
                skillRange = range;

                // Move em direção ao monstro
                SendMoveRequestToServer(monsterObj.transform.position);
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog($"<color=cyan>🏃 Aproximando do alvo...</color>");
                }
                
                return;
            }
        }

        // ✅ ESTÁ NO RANGE OU NÃO PRECISA DE TARGET - USA A SKILL
        ExecuteSkill(skillId, slotNumber, skill.template);
    }

    /// <summary>
    /// ✅ NOVO - Verifica se chegou no range e usa a skill
    /// </summary>
    private void CheckSkillRangeAndUse()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        
        if (player == null)
        {
            movingToUseSkill = false;
            return;
        }

        float distance = Vector3.Distance(player.transform.position, targetPositionForSkill);

        // Chegou no range?
        if (distance <= skillRange)
        {
            Debug.Log($"✅ Reached skill range! Using skill {pendingSkillId}");
            
            if (learnedSkills.TryGetValue(pendingSkillId, out var skill))
            {
                ExecuteSkill(pendingSkillId, pendingSlotNumber, skill.template);
            }
            
            movingToUseSkill = false;
            pendingSkillId = 0;
        }
    }

    /// <summary>
    /// ✅ NOVO - Executa a skill (envia para servidor)
    /// </summary>
    private void ExecuteSkill(int skillId, int slotNumber, SkillTemplateData template)
    {
        // Determina target baseado no tipo de skill
        string targetId = null;
        Vector3? targetPosition = null;

        switch (template.targetType)
        {
            case "enemy":
                if (currentTargetMonsterId > 0)
                {
                    targetId = currentTargetMonsterId.ToString();
                }
                break;

            case "self":
                targetId = ClientManager.Instance.PlayerId;
                break;

            case "area":
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    targetPosition = player.transform.position;
                }
                break;

            case "ally":
                targetId = ClientManager.Instance.PlayerId;
                break;
        }

        // Monta mensagem
        var message = new
        {
            type = "useSkill",
            skillId = skillId,
            slotNumber = slotNumber,
            targetId = targetId,
            targetType = targetId != null ? "monster" : "player",
            targetPosition = targetPosition != null ? new
            {
                x = targetPosition.Value.x,
                y = targetPosition.Value.y,
                z = targetPosition.Value.z
            } : null
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);

        Debug.Log($"⚔️ Using skill: {template.name} (Level {learnedSkills[skillId].currentLevel})");
    }

    /// <summary>
    /// ✅ HELPER - Busca monstro por ID na cena
    /// </summary>
    private GameObject FindMonsterByIdInScene(int monsterId)
    {
        var monsters = GameObject.FindGameObjectsWithTag("Monster");
        
        foreach (var monsterObj in monsters)
        {
            var controller = monsterObj.GetComponent<MonsterController>();
            
            if (controller != null && controller.monsterId == monsterId)
            {
                return monsterObj;
            }
        }
        
        return null;
    }

    private void SendMoveRequestToServer(Vector3 targetPosition)
    {
        if (TerrainHelper.Instance != null)
        {
            targetPosition = TerrainHelper.Instance.ClampToGround(targetPosition, 0f);
        }

        var message = new
        {
            type = "moveRequest",
            targetPosition = new
            {
                x = targetPosition.x,
                y = targetPosition.y,
                z = targetPosition.z
            }
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    public void LearnSkill(int skillId, int slotNumber)
    {
        var message = new
        {
            type = "learnSkill",
            skillId = skillId,
            slotNumber = slotNumber
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    public void LevelUpSkill(int skillId)
    {
        var message = new
        {
            type = "levelUpSkill",
            skillId = skillId
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    public void PlaySkillEffect(int skillId, Vector3 position, string targetType)
    {
        if (!learnedSkills.TryGetValue(skillId, out var skill))
            return;

        if (skill.template == null)
            return;

        GameObject effectPrefab = null;
        
        if (!string.IsNullOrEmpty(skill.template.effectPrefab))
        {
            effectPrefab = Resources.Load<GameObject>(skill.template.effectPrefab);
        }

        if (effectPrefab == null)
        {
            effectPrefab = defaultSkillEffectPrefab;
        }

        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        if (!string.IsNullOrEmpty(skill.template.soundEffect))
        {
            Debug.Log($"🔊 Playing sound: {skill.template.soundEffect}");
        }
    }

    private void OnDestroy()
    {
        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnSelectCharacterResponse -= HandleCharacterSelected;
        }
    }
}
