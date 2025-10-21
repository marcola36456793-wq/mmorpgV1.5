using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
/// Gerenciador de skills no cliente Unity - VERSÃO CORRIGIDA
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
    
    // ✅ CORRIGIDO - Cache de target atual
    private int currentTargetMonsterId = -1;

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
        
        // ✅ NOVO - Atualiza target quando jogador ataca monstro
        if (UIManager.Instance != null)
        {
            // Pode adicionar evento se necessário
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

    /// <summary>
    /// ✅ CORRIGIDO - Atualiza target atual
    /// </summary>
    public void SetCurrentTarget(int monsterId)
    {
        currentTargetMonsterId = monsterId;
        Debug.Log($"🎯 Target set: Monster ID {monsterId}");
    }

    /// <summary>
    /// ✅ CORRIGIDO - Limpa target
    /// </summary>
    public void ClearCurrentTarget()
    {
        currentTargetMonsterId = -1;
        Debug.Log($"🎯 Target cleared");
    }

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

        // Determina target baseado no tipo de skill
        string targetId = null;
        Vector3? targetPosition = null;

        switch (skill.template.targetType)
        {
            case "enemy":
                // ✅ CORRIGIDO - Pega do cache
                if (currentTargetMonsterId > 0)
                {
                    targetId = currentTargetMonsterId.ToString();
                }
                else
                {
                    Debug.Log("❌ Nenhum alvo selecionado!");
                    
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.AddCombatLog("<color=yellow>❌ Selecione um alvo primeiro!</color>");
                    }
                    return;
                }
                break;

            case "self":
                targetId = ClientManager.Instance.PlayerId;
                break;

            case "area":
                targetPosition = GetPlayerPosition();
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

        Debug.Log($"⚔️ Using skill: {skill.template.name} (Level {skill.currentLevel})");
    }

    private Vector3? GetPlayerPosition()
    {
        var localPlayer = GameObject.FindGameObjectWithTag("Player");
        
        if (localPlayer != null)
        {
            return localPlayer.transform.position;
        }

        return null;
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
