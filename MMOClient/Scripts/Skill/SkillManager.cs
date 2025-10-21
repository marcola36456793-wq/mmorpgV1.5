using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gerencia todas as skills do jogador local
/// Coloque em: Assets/Scripts/Skills/SkillManager.cs
/// </summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [Header("Skill Database")]
    public List<SkillData> allSkills = new List<SkillData>();

    [Header("Hotbar")]
    public SkillSlotUI[] hotbarSlots = new SkillSlotUI[9]; // Teclas 1-9

    [Header("Targeting")]
    public GameObject aoeIndicatorInstance;
    public LayerMask groundLayer;
    public LayerMask monsterLayer;

    // Skills conhecidas pelo player
    private Dictionary<int, SkillData> learnedSkills = new Dictionary<int, SkillData>();
    
    // Cooldowns ativos
    private Dictionary<int, float> cooldowns = new Dictionary<int, float>();
    
    // Estado de casting
    private bool isCasting = false;
    private float castTimer = 0f;
    private SkillData currentSkill;
    private object currentTarget;

    // Targeting
    private bool isTargeting = false;
    private SkillData targetingSkill;
    private MonsterController selectedMonster;

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
        // Registra eventos do servidor
        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnMessageReceived += HandleServerMessage;
        }

        // Carrega hotbar salva
        LoadHotbar();
    }

    private void Update()
    {
        // Atualiza cooldowns
        UpdateCooldowns();

        // Atualiza casting
        if (isCasting)
        {
            UpdateCasting();
        }

        // Atualiza targeting
        if (isTargeting)
        {
            UpdateTargeting();
        }

        // Hotkeys (1-9)
        CheckHotkeys();
    }

    // ==================== HOTKEYS ====================

    private void CheckHotkeys()
    {
        if (isCasting || isTargeting)
            return;

        for (int i = 0; i < 9; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;
            
            if (Input.GetKeyDown(key))
            {
                UseSkillFromHotbar(i);
            }
        }
    }

    public void UseSkillFromHotbar(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= hotbarSlots.Length)
            return;

        var slot = hotbarSlots[slotIndex];
        
        if (slot == null || slot.skillData == null)
        {
            Debug.Log("⚠️ Slot vazio");
            return;
        }

        UseSkill(slot.skillData);
    }

    // ==================== USO DE SKILLS ====================

    public void UseSkill(SkillData skill)
    {
        if (skill == null)
            return;

        // Validações básicas (cliente)
        if (isCasting)
        {
            ShowSkillMessage("Já está usando uma skill!");
            return;
        }

        if (IsOnCooldown(skill.skillId))
        {
            float remaining = GetCooldownRemaining(skill.skillId);
            ShowSkillMessage($"Aguarde {remaining:F1}s");
            return;
        }

        // Verifica tipo de targeting
        switch (skill.targetType)
        {
            case TargetType.Single:
                StartSingleTargeting(skill);
                break;

            case TargetType.Ground:
                StartGroundTargeting(skill);
                break;

            case TargetType.Self:
            case TargetType.NoTarget:
                CastSkillImmediately(skill, null);
                break;

            case TargetType.Direction:
                CastSkillInDirection(skill);
                break;
        }
    }

    // ==================== TARGETING SINGLE ====================

    private void StartSingleTargeting(SkillData skill)
    {
        // Se já tem monstro selecionado, usa nele
        if (selectedMonster != null && selectedMonster.isAlive)
        {
            CastSkillOnTarget(skill, selectedMonster.monsterId);
        }
        else
        {
            // Entra em modo de seleção
            isTargeting = true;
            targetingSkill = skill;
            ShowSkillMessage($"Selecione um alvo para {skill.skillName}");
        }
    }

    private void UpdateTargeting()
    {
        // ESC para cancelar
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTargeting();
            return;
        }

        // Clique esquerdo para selecionar
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f, monsterLayer))
            {
                var monster = hit.collider.GetComponent<MonsterController>();
                
                if (monster != null && monster.isAlive)
                {
                    selectedMonster = monster;
                    CastSkillOnTarget(targetingSkill, monster.monsterId);
                    isTargeting = false;
                    targetingSkill = null;
                }
            }
        }
    }

    private void CancelTargeting()
    {
        isTargeting = false;
        targetingSkill = null;
        HideAOEIndicator();
        ShowSkillMessage("Cancelado");
    }

    // ==================== TARGETING GROUND (AOE) ====================

    private void StartGroundTargeting(SkillData skill)
    {
        isTargeting = true;
        targetingSkill = skill;
        ShowAOEIndicator(skill.aoeRadius);
        ShowSkillMessage($"Clique no chão para usar {skill.skillName}");
    }

    private void UpdateGroundTargeting()
    {
        // ESC para cancelar
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTargeting();
            return;
        }

        // Move indicador com mouse
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
        {
            if (aoeIndicatorInstance != null)
            {
                aoeIndicatorInstance.transform.position = hit.point;
            }

            // Clique para confirmar
            if (Input.GetMouseButtonDown(0))
            {
                CastSkillOnGround(targetingSkill, hit.point);
                isTargeting = false;
                targetingSkill = null;
                HideAOEIndicator();
            }
        }
    }

    // ==================== CAST ====================

    private void CastSkillImmediately(SkillData skill, object target)
    {
        StartCast(skill, target);
    }

    private void CastSkillOnTarget(SkillData skill, int targetId)
    {
        currentTarget = targetId;
        StartCast(skill, targetId);
    }

    private void CastSkillOnGround(SkillData skill, Vector3 position)
    {
        currentTarget = position;
        StartCast(skill, position);
    }

    private void CastSkillInDirection(SkillData skill)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Vector3 direction = ray.direction;
        currentTarget = direction;
        StartCast(skill, direction);
    }

    private void StartCast(SkillData skill, object target)
    {
        currentSkill = skill;
        currentTarget = target;

        // Cast time = 0 → executa imediatamente
        if (skill.castTime <= 0f)
        {
            ExecuteSkill();
        }
        else
        {
            // Inicia casting
            isCasting = true;
            castTimer = skill.castTime;

            // Animação de cast
            PlayCastAnimation(skill);

            // Efeito visual de cast
            if (skill.castEffectPrefab != null)
            {
                Instantiate(skill.castEffectPrefab, transform.position, Quaternion.identity, transform);
            }

            ShowSkillMessage($"Usando {skill.skillName}...");
        }
    }

    private void UpdateCasting()
    {
        castTimer -= Time.deltaTime;

        if (castTimer <= 0f)
        {
            ExecuteSkill();
        }
    }

    private void ExecuteSkill()
    {
        isCasting = false;

        if (currentSkill == null)
            return;

        // Envia para servidor
        SendSkillToServer(currentSkill, currentTarget);

        // Inicia cooldown (visual)
        StartCooldown(currentSkill.skillId, currentSkill.cooldown);

        // Animação de execução
        PlaySkillAnimation(currentSkill);

        // Som
        if (currentSkill.castSound != null)
        {
            AudioSource.PlayClipAtPoint(currentSkill.castSound, transform.position);
        }

        currentSkill = null;
        currentTarget = null;
    }

    // ==================== COMUNICAÇÃO COM SERVIDOR ====================

    private void SendSkillToServer(SkillData skill, object target)
    {
        var message = new
        {
            type = "castSkill",
            skillId = skill.skillId,
            targetId = target is int ? (int)target : 0,
            targetPosition = target is Vector3 pos ? new { x = pos.x, y = pos.y, z = pos.z } : null
        };

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);

        Debug.Log($"⚡ Cast {skill.skillName} → Server");
    }

    private void HandleServerMessage(string message)
    {
        try
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(message);
            var type = json["type"]?.ToString();

            switch (type)
            {
                case "skillCast":
                case "skillCastBroadcast":
                    HandleSkillCastResult(json);
                    break;

                case "learnSkillResponse":
                    HandleLearnSkillResponse(json);
                    break;

                case "skillsResponse":
                    HandleSkillsReceived(json);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error handling skill message: {ex.Message}");
        }
    }

    private void HandleSkillCastResult(Newtonsoft.Json.Linq.JObject json)
    {
        bool success = json["success"]?.ToObject<bool>() ?? false;
        string skillName = json["skillName"]?.ToString() ?? "";

        if (!success)
        {
            string reason = json["failReason"]?.ToString() ?? "Falhou";
            ShowSkillMessage($"❌ {skillName}: {TranslateFailReason(reason)}");
            return;
        }

        // Skill executada com sucesso
        var targetResults = json["targetResults"];
        
        if (targetResults != null)
        {
            foreach (var result in targetResults)
            {
                int damage = result["damage"]?.ToObject<int>() ?? 0;
                int heal = result["heal"]?.ToObject<int>() ?? 0;
                bool isCrit = result["isCritical"]?.ToObject<bool>() ?? false;
                string targetName = result["targetName"]?.ToString() ?? "";

                if (damage > 0)
                {
                    string critText = isCrit ? " CRÍTICO!" : "";
                    ShowSkillMessage($"⚡ {skillName} → {targetName}: {damage}{critText}");
                }
                
                if (heal > 0)
                {
                    ShowSkillMessage($"💚 {skillName}: +{heal} HP");
                }
            }
        }
    }

    private void HandleLearnSkillResponse(Newtonsoft.Json.Linq.JObject json)
    {
        bool success = json["success"]?.ToObject<bool>() ?? false;
        string message = json["message"]?.ToString() ?? "";

        if (success)
        {
            var skillData = json["skill"];
            int skillId = skillData["id"]?.ToObject<int>() ?? 0;
            
            // Adiciona à lista de skills conhecidas
            var skill = allSkills.FirstOrDefault(s => s.skillId == skillId);
            
            if (skill != null && !learnedSkills.ContainsKey(skillId))
            {
                learnedSkills[skillId] = skill;
                Debug.Log($"✅ Learned skill: {skill.skillName}");
            }
        }

        ShowSkillMessage(message);
    }

    private void HandleSkillsReceived(Newtonsoft.Json.Linq.JObject json)
    {
        learnedSkills.Clear();

        var skills = json["skills"];
        
        if (skills != null)
        {
            foreach (var skillJson in skills)
            {
                int skillId = skillJson["id"]?.ToObject<int>() ?? 0;
                var skill = allSkills.FirstOrDefault(s => s.skillId == skillId);
                
                if (skill != null)
                {
                    learnedSkills[skillId] = skill;
                }
            }
        }

        Debug.Log($"📚 Loaded {learnedSkills.Count} skills");
    }

    // ==================== COOLDOWNS ====================

    private void UpdateCooldowns()
    {
        var keys = cooldowns.Keys.ToList();
        
        foreach (var skillId in keys)
        {
            cooldowns[skillId] -= Time.deltaTime;
            
            if (cooldowns[skillId] <= 0f)
            {
                cooldowns.Remove(skillId);
                
                // Atualiza UI
                UpdateHotbarCooldown(skillId, 0f);
            }
            else
            {
                UpdateHotbarCooldown(skillId, cooldowns[skillId]);
            }
        }
    }

    public void StartCooldown(int skillId, float duration)
    {
        cooldowns[skillId] = duration;
    }

    public bool IsOnCooldown(int skillId)
    {
        return cooldowns.ContainsKey(skillId);
    }

    public float GetCooldownRemaining(int skillId)
    {
        return cooldowns.ContainsKey(skillId) ? cooldowns[skillId] : 0f;
    }

    private void UpdateHotbarCooldown(int skillId, float remaining)
    {
        foreach (var slot in hotbarSlots)
        {
            if (slot != null && slot.skillData != null && slot.skillData.skillId == skillId)
            {
                slot.UpdateCooldown(remaining);
            }
        }
    }

    // ==================== HOTBAR MANAGEMENT ====================

    public void AssignSkillToHotbar(SkillData skill, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= hotbarSlots.Length)
            return;

        if (!learnedSkills.ContainsKey(skill.skillId))
        {
            ShowSkillMessage("Você não conhece esta skill!");
            return;
        }

        hotbarSlots[slotIndex].SetSkill(skill);
        SaveHotbar();
    }

    public void ClearHotbarSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < hotbarSlots.Length)
        {
            hotbarSlots[slotIndex].ClearSlot();
            SaveHotbar();
        }
    }

    private void SaveHotbar()
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i]?.skillData != null)
            {
                PlayerPrefs.SetInt($"Hotbar_{i}", hotbarSlots[i].skillData.skillId);
            }
            else
            {
                PlayerPrefs.DeleteKey($"Hotbar_{i}");
            }
        }
        PlayerPrefs.Save();
    }

    private void LoadHotbar()
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (PlayerPrefs.HasKey($"Hotbar_{i}"))
            {
                int skillId = PlayerPrefs.GetInt($"Hotbar_{i}");
                var skill = allSkills.FirstOrDefault(s => s.skillId == skillId);
                
                if (skill != null && hotbarSlots[i] != null)
                {
                    hotbarSlots[i].SetSkill(skill);
                }
            }
        }
    }

    // ==================== VISUAL FEEDBACK ====================

    private void ShowAOEIndicator(float radius)
    {
        if (aoeIndicatorInstance == null && SkillEffectManager.Instance != null)
        {
            aoeIndicatorInstance = SkillEffectManager.Instance.CreateAOEIndicator(radius);
        }

        if (aoeIndicatorInstance != null)
        {
            aoeIndicatorInstance.SetActive(true);
        }
    }

    private void HideAOEIndicator()
    {
        if (aoeIndicatorInstance != null)
        {
            aoeIndicatorInstance.SetActive(false);
        }
    }

    private void PlayCastAnimation(SkillData skill)
    {
        var animator = GetComponent<Animator>();
        
        if (animator != null && !string.IsNullOrEmpty(skill.animationTrigger))
        {
            animator.SetTrigger(skill.animationTrigger);
        }
    }

    private void PlaySkillAnimation(SkillData skill)
    {
        // Aqui você pode tocar animações específicas
    }

    private void ShowSkillMessage(string message)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog(message);
        }
    }

    private string TranslateFailReason(string reason)
    {
        return reason switch
        {
            "CASTER_NOT_FOUND" => "Player não encontrado",
            "SKILL_NOT_FOUND" => "Skill não existe",
            "CASTER_DEAD" => "Você está morto!",
            "SKILL_NOT_LEARNED" => "Você não aprendeu esta skill",
            "INSUFFICIENT_MANA" => "Mana insuficiente",
            "INSUFFICIENT_HEALTH" => "HP insuficiente",
            "ON_COOLDOWN" => "Aguarde o cooldown",
            "TARGET_NOT_FOUND" => "Alvo não encontrado",
            "OUT_OF_RANGE" => "Muito longe!",
            _ => reason
        };
    }

    // ==================== PUBLIC API ====================

    public List<SkillData> GetLearnedSkills()
    {
        return learnedSkills.Values.ToList();
    }

    public void RequestSkillsFromServer()
    {
        var message = new { type = "getSkills" };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    private void OnDestroy()
    {
        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnMessageReceived -= HandleServerMessage;
        }
    }
}