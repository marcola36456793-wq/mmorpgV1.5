using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
/// Gerenciador de skills no cliente Unity
/// Coloque em: MMOClient/Scripts/UI/Skills/SkillManager.cs
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
    
    // Cache de targets
    private string currentTargetId = null;
    private string currentTargetType = "monster";

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

    /// <summary>
    /// Cria os 9 slots da hotbar
    /// </summary>
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

    /// <summary>
    /// Registra handlers de mensagens do servidor
    /// </summary>
    private void RegisterMessageHandlers()
    {
        if (MessageHandler.Instance != null)
        {
            // Quando seleciona personagem, carrega skills
            MessageHandler.Instance.OnSelectCharacterResponse += HandleCharacterSelected;
        }
    }

    /// <summary>
    /// Carrega skills do personagem
    /// </summary>
    private void HandleCharacterSelected(SelectCharacterResponseData data)
    {
        if (data.success && data.character != null)
        {
            // Solicita skills do servidor
            RequestSkills();
        }
    }

    /// <summary>
    /// Solicita lista de skills do servidor
    /// </summary>
    public void RequestSkills()
    {
        var message = new
        {
            type = "getSkills"
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    /// <summary>
    /// Atualiza skills na hotbar (chamado quando servidor responde)
    /// </summary>
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

    /// <summary>
    /// Atualiza visual da hotbar
    /// </summary>
    private void RefreshHotbar()
    {
        // Limpa todos os slots
        foreach (var slot in skillSlots)
        {
            slot.Clear();
        }

        // Preenche slots com skills aprendidas
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
    /// Usa skill (chamado pelo slot ou hotkey)
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

        // Determina target baseado no tipo de skill
        string targetId = null;
        Vector3? targetPosition = null;

        switch (skill.template.targetType)
        {
            case "enemy":
                // Precisa de um monstro selecionado
                targetId = GetCurrentMonsterTarget();
                if (string.IsNullOrEmpty(targetId))
                {
                    Debug.Log("❌ Selecione um alvo!");
                    return;
                }
                currentTargetType = "monster";
                break;

            case "self":
                // Usa em si mesmo
                targetId = ClientManager.Instance.PlayerId;
                currentTargetType = "player";
                break;

            case "area":
                // Skill de área - usa posição do player ou cursor
                targetPosition = GetPlayerPosition();
                break;

            case "ally":
                // Por enquanto, usa em si mesmo (futuramente party system)
                targetId = ClientManager.Instance.PlayerId;
                currentTargetType = "player";
                break;
        }

        // Monta mensagem
        var message = new
        {
            type = "useSkill",
            skillId = skillId,
            slotNumber = slotNumber,
            targetId = targetId,
            targetType = currentTargetType,
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

    /// <summary>
    /// Obtém ID do monstro selecionado
    /// </summary>
    private string GetCurrentMonsterTarget()
    {
        // Pega do UIManager (target panel)
        if (UIManager.Instance != null)
        {
            // Assumindo que UIManager tem referência ao monstro atual
            // Você pode adicionar um método público no UIManager
            // Por enquanto, retorna null (você precisa integrar com seu sistema de targeting)
            return null;
        }

        return null;
    }

    /// <summary>
    /// Obtém posição do player
    /// </summary>
    private Vector3? GetPlayerPosition()
    {
        var localPlayer = GameObject.FindGameObjectWithTag("Player");
        
        if (localPlayer != null)
        {
            return localPlayer.transform.position;
        }

        return null;
    }

    /// <summary>
    /// Aprende uma nova skill (chamado pela UI de skills)
    /// </summary>
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

    /// <summary>
    /// Aumenta nível de uma skill
    /// </summary>
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

    /// <summary>
    /// Toca efeito visual da skill
    /// </summary>
    public void PlaySkillEffect(int skillId, Vector3 position, string targetType)
    {
        if (!learnedSkills.TryGetValue(skillId, out var skill))
            return;

        if (skill.template == null)
            return;

        // Carrega prefab do efeito
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

        // Toca som
        if (!string.IsNullOrEmpty(skill.template.soundEffect))
        {
            // TODO: Integrar com seu sistema de áudio
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