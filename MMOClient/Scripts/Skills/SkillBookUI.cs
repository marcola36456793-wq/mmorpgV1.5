using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Interface para gerenciar skills (aprender e upar)
/// VERSÃO CORRIGIDA - Usa SkillEntryUI
/// </summary>
public class SkillBookUI : MonoBehaviour
{
    public static SkillBookUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject skillBookPanel;
    
    [Header("Learned Skills")]
    public Transform learnedSkillsContainer;
    public GameObject learnedSkillEntryPrefab; // ✅ Agora com SkillEntryUI
    
    [Header("Available Skills")]
    public Transform availableSkillsContainer;
    public GameObject availableSkillEntryPrefab; // ✅ Agora com SkillEntryUI
    
    [Header("Info Panel")]
    public GameObject skillInfoPanel;
    public TextMeshProUGUI skillInfoName;
    public TextMeshProUGUI skillInfoDescription;
    public TextMeshProUGUI skillInfoStats;
    public Button learnButton;
    public Button levelUpButton;
    public Button assignSlotButton;
    
    [Header("Slot Selection")]
    public GameObject slotSelectionPanel;
    public Transform slotButtonsContainer;
    public GameObject slotButtonPrefab; // ✅ Prefab para botões de slot
    
    [Header("Status")]
    public TextMeshProUGUI statusPointsText;
    
    private List<LearnedSkillData> learnedSkills = new List<LearnedSkillData>();
    private List<SkillTemplateData> availableSkills = new List<SkillTemplateData>();
    private SkillEntryUI selectedEntry;
    private bool isVisible = false;

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
        if (skillBookPanel != null)
            skillBookPanel.SetActive(false);
        
        if (slotSelectionPanel != null)
            slotSelectionPanel.SetActive(false);

        // Configura botões
        if (learnButton != null)
            learnButton.onClick.AddListener(OnLearnButtonClick);
        
        if (levelUpButton != null)
            levelUpButton.onClick.AddListener(OnLevelUpButtonClick);
        
        if (assignSlotButton != null)
            assignSlotButton.onClick.AddListener(OnAssignSlotButtonClick);

        HideSkillInfo();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (isVisible)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        if (skillBookPanel != null)
            skillBookPanel.SetActive(true);
        
        isVisible = true;
        RequestSkillData();
    }

    public void Hide()
    {
        if (skillBookPanel != null)
            skillBookPanel.SetActive(false);
        
        isVisible = false;
    }

    private void RequestSkillData()
    {
        // Skills aprendidas
        if (SkillManager.Instance != null)
            SkillManager.Instance.RequestSkills();
        
        // Skills disponíveis
        var message = new
        {
            type = "getSkillList"
        };

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    /// <summary>
    /// ✅ CORRIGIDO - Usa SkillEntryUI
    /// </summary>
    public void UpdateLearnedSkills(List<LearnedSkillData> skills)
    {
        learnedSkills = skills;
        RefreshLearnedSkillsList();
        UpdateStatusPoints();
    }

    /// <summary>
    /// ✅ CORRIGIDO - Usa SkillEntryUI
    /// </summary>
    public void UpdateAvailableSkills(List<SkillTemplateData> skills)
    {
        availableSkills = skills;
        RefreshAvailableSkillsList();
    }

    private void RefreshLearnedSkillsList()
    {
        // Limpa lista
        foreach (Transform child in learnedSkillsContainer)
        {
            Destroy(child.gameObject);
        }

        // Preenche com skills aprendidas
        foreach (var skill in learnedSkills)
        {
            if (skill.template == null)
                continue;

            GameObject entry = Instantiate(learnedSkillEntryPrefab, learnedSkillsContainer);
            SkillEntryUI entryUI = entry.GetComponent<SkillEntryUI>();
            
            if (entryUI != null)
            {
                entryUI.SetLearnedSkill(skill);
                entryUI.OnClicked += OnSkillEntryClicked;
            }
        }
    }

    private void RefreshAvailableSkillsList()
    {
        // Limpa lista
        foreach (Transform child in availableSkillsContainer)
        {
            Destroy(child.gameObject);
        }

        var charData = WorldManager.Instance.GetLocalCharacterData();
        if (charData == null)
            return;

        // Preenche com skills disponíveis
        foreach (var skill in availableSkills)
        {
            GameObject entry = Instantiate(availableSkillEntryPrefab, availableSkillsContainer);
            SkillEntryUI entryUI = entry.GetComponent<SkillEntryUI>();
            
            if (entryUI != null)
            {
                bool canLearn = charData.level >= skill.requiredLevel;
                entryUI.SetAvailableSkill(skill, canLearn);
                entryUI.OnClicked += OnSkillEntryClicked;
            }
        }
    }

    /// <summary>
    /// ✅ CORRIGIDO - Handler unificado
    /// </summary>
    private void OnSkillEntryClicked(SkillEntryUI entry)
    {
        // Deseleciona anterior
        if (selectedEntry != null)
        {
            selectedEntry.SetSelected(false);
        }

        // Seleciona novo
        selectedEntry = entry;
        entry.SetSelected(true);

        // Mostra info
        if (entry.IsLearned())
        {
            var skill = entry.GetLearnedSkill();
            if (skill != null && skill.template != null)
            {
                ShowSkillInfo(skill.template, skill);
            }
        }
        else
        {
            var skill = entry.GetAvailableSkill();
            if (skill != null)
            {
                ShowSkillInfo(skill, null);
            }
        }
    }

    private void ShowSkillInfo(SkillTemplateData template, LearnedSkillData learnedData)
    {
        if (skillInfoPanel != null)
            skillInfoPanel.SetActive(true);

        if (skillInfoName != null)
        {
            string levelInfo = learnedData != null ? $" (Lv. {learnedData.currentLevel}/{template.maxLevel})" : "";
            skillInfoName.text = template.name + levelInfo;
        }

        if (skillInfoDescription != null)
        {
            skillInfoDescription.text = template.description;
        }

        if (skillInfoStats != null)
        {
            skillInfoStats.text = BuildDetailedStats(template, learnedData);
        }

        UpdateInfoButtons(template, learnedData);
    }

    private void HideSkillInfo()
    {
        if (skillInfoPanel != null)
            skillInfoPanel.SetActive(false);
    }

    private string BuildDetailedStats(SkillTemplateData template, LearnedSkillData learnedData)
    {
        string stats = "";

        stats += $"<b>Tipo:</b> {TranslateSkillType(template.skillType)}\n";
        stats += $"<b>Dano:</b> {TranslateDamageType(template.damageType)}\n";
        stats += $"<b>Alvo:</b> {TranslateTargetType(template.targetType)}\n\n";

        if (template.range > 0)
            stats += $"<color=cyan>Alcance:</color> {template.range}m\n";

        if (template.areaRadius > 0)
            stats += $"<color=cyan>Área:</color> {template.areaRadius}m\n";

        stats += $"<color=blue>Custo Mana:</color> {template.manaCost}\n";

        if (template.healthCost > 0)
            stats += $"<color=red>Custo HP:</color> {template.healthCost}\n";

        stats += $"<color=orange>Cooldown:</color> {template.cooldown}s\n";

        if (template.castTime > 0)
            stats += $"<color=gray>Conjuração:</color> {template.castTime}s\n";

        stats += "\n";

        int currentLevel = learnedData?.currentLevel ?? 1;
        var levelData = GetLevelData(template, currentLevel);

        if (levelData != null)
        {
            stats += $"<b><color=yellow>Nível {currentLevel}:</color></b>\n";

            if (levelData.baseDamage > 0)
            {
                stats += $"  Dano Base: {levelData.baseDamage}\n";
                stats += $"  Multiplicador: {levelData.damageMultiplier * 100}%\n";
            }

            if (levelData.baseHealing > 0)
            {
                stats += $"  Cura Base: {levelData.baseHealing}\n";
            }

            if (levelData.critChanceBonus > 0)
            {
                stats += $"  Bônus Crítico: +{levelData.critChanceBonus * 100}%\n";
            }
        }

        return stats;
    }

    private SkillLevelData GetLevelData(SkillTemplateData template, int level)
    {
        if (template.levels == null || template.levels.Length == 0)
            return null;

        foreach (var data in template.levels)
        {
            if (data.level == level)
                return data;
        }

        return null;
    }

    private void UpdateInfoButtons(SkillTemplateData template, LearnedSkillData learnedData)
    {
        var charData = WorldManager.Instance.GetLocalCharacterData();
        
        if (charData == null)
            return;

        // Botão de aprender
        if (learnButton != null)
        {
            bool showLearn = learnedData == null;
            bool canLearn = charData.level >= template.requiredLevel;
            
            learnButton.gameObject.SetActive(showLearn);
            learnButton.interactable = canLearn;
        }

        // Botão de upar
        if (levelUpButton != null)
        {
            bool showLevelUp = learnedData != null && learnedData.currentLevel < template.maxLevel;
            bool canLevelUp = showLevelUp && charData.statusPoints > 0;
            
            levelUpButton.gameObject.SetActive(showLevelUp);
            levelUpButton.interactable = canLevelUp;
        }

        // Botão de atribuir slot
        if (assignSlotButton != null)
        {
            bool showAssign = learnedData != null;
            assignSlotButton.gameObject.SetActive(showAssign);
        }
    }

    private void UpdateStatusPoints()
    {
        if (statusPointsText != null)
        {
            var charData = WorldManager.Instance.GetLocalCharacterData();
            int points = charData?.statusPoints ?? 0;
            statusPointsText.text = $"Pontos de Skill: {points}";
        }
    }

    // ==================== BOTÕES ====================

    private void OnLearnButtonClick()
    {
        if (selectedEntry == null || !selectedEntry.IsLearned())
        {
            var skill = selectedEntry?.GetAvailableSkill();
            if (skill != null)
            {
                ShowSlotSelection();
            }
        }
    }

    private void OnLevelUpButtonClick()
    {
        if (selectedEntry == null || !selectedEntry.IsLearned())
            return;

        var skill = selectedEntry.GetLearnedSkill();
        if (skill != null)
        {
            SkillManager.Instance?.LevelUpSkill(skill.skillId);
        }
    }

    private void OnAssignSlotButtonClick()
    {
        if (selectedEntry == null || !selectedEntry.IsLearned())
            return;

        ShowSlotSelection();
    }

    private void ShowSlotSelection()
    {
        if (slotSelectionPanel != null)
        {
            slotSelectionPanel.SetActive(true);
            CreateSlotButtons();
        }
    }

    private void CreateSlotButtons()
    {
        // Limpa botões existentes
        foreach (Transform child in slotButtonsContainer)
        {
            Destroy(child.gameObject);
        }

        // Cria botões de 1 a 9
        for (int i = 1; i <= 9; i++)
        {
            int slotNumber = i;
            
            GameObject buttonObj;
            
            if (slotButtonPrefab != null)
            {
                buttonObj = Instantiate(slotButtonPrefab, slotButtonsContainer);
            }
            else
            {
                // Fallback: cria botão simples
                buttonObj = new GameObject($"SlotButton_{i}");
                buttonObj.transform.SetParent(slotButtonsContainer);
                buttonObj.AddComponent<Image>();
                
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform);
                TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
                text.text = i.ToString();
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 24;
            }
            
            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
                button = buttonObj.AddComponent<Button>();
            
            button.onClick.AddListener(() => OnSlotSelected(slotNumber));
        }
    }

    private void OnSlotSelected(int slotNumber)
    {
        if (slotSelectionPanel != null)
            slotSelectionPanel.SetActive(false);

        if (selectedEntry == null)
            return;

        // Aprender skill nova
        if (!selectedEntry.IsLearned())
        {
            var skill = selectedEntry.GetAvailableSkill();
            if (skill != null)
            {
                SkillManager.Instance?.LearnSkill(skill.id, slotNumber);
            }
        }
        // Reatribuir slot de skill existente
        else
        {
            var skill = selectedEntry.GetLearnedSkill();
            if (skill != null)
            {
                // TODO: Implementar reatribuição de slot no servidor
                Debug.Log($"Reatribuir skill {skill.skillId} para slot {slotNumber}");
            }
        }
    }

    // ==================== HELPERS ====================

    private string TranslateSkillType(string type)
    {
        return type switch
        {
            "active" => "Ativa",
            "passive" => "Passiva",
            "buff" => "Buff",
            _ => type
        };
    }

    private string TranslateDamageType(string type)
    {
        return type switch
        {
            "physical" => "Físico",
            "magical" => "Mágico",
            "true" => "Verdadeiro",
            "none" => "Nenhum",
            _ => type
        };
    }

    private string TranslateTargetType(string type)
    {
        return type switch
        {
            "enemy" => "Inimigo",
            "self" => "Próprio",
            "ally" => "Aliado",
            "area" => "Área",
            _ => type
        };
    }
}
