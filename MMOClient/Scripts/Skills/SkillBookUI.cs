using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Interface para gerenciar skills (aprender e upar)
/// Coloque em: MMOClient/Scripts/UI/Skills/SkillBookUI.cs
/// </summary>
public class SkillBookUI : MonoBehaviour
{
    public static SkillBookUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject skillBookPanel;
    
    [Header("Learned Skills")]
    public Transform learnedSkillsContainer;
    public GameObject learnedSkillEntryPrefab;
    
    [Header("Available Skills")]
    public Transform availableSkillsContainer;
    public GameObject availableSkillEntryPrefab;
    
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
    
    [Header("Status")]
    public TextMeshProUGUI statusPointsText;
    
    private List<LearnedSkillData> learnedSkills = new List<LearnedSkillData>();
    private List<SkillTemplateData> availableSkills = new List<SkillTemplateData>();
    private LearnedSkillData selectedLearnedSkill;
    private SkillTemplateData selectedAvailableSkill;
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
    }

    private void Update()
    {
        // Hotkey para abrir (K = Skills)
        if (Input.GetKeyDown(KeyCode.K))
        {
            Toggle();
        }
    }

    /// <summary>
    /// Abre/fecha o livro de skills
    /// </summary>
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
        
        // Solicita dados atualizados
        RequestSkillData();
    }

    public void Hide()
    {
        if (skillBookPanel != null)
            skillBookPanel.SetActive(false);
        
        isVisible = false;
    }

    /// <summary>
    /// Solicita dados de skills do servidor
    /// </summary>
    private void RequestSkillData()
    {
        // Skills aprendidas
        SkillManager.Instance?.RequestSkills();
        
        // Skills disponíveis para aprender
        var message = new
        {
            type = "getSkillList"
        };

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    /// <summary>
    /// Atualiza lista de skills aprendidas
    /// </summary>
    public void UpdateLearnedSkills(List<LearnedSkillData> skills)
    {
        learnedSkills = skills;
        RefreshLearnedSkillsList();
        UpdateStatusPoints();
    }

    /// <summary>
    /// Atualiza lista de skills disponíveis
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
            
            // Configura entry
            ConfigureLearnedSkillEntry(entry, skill);
        }
    }

    private void RefreshAvailableSkillsList()
    {
        // Limpa lista
        foreach (Transform child in availableSkillsContainer)
        {
            Destroy(child.gameObject);
        }

        // Preenche com skills disponíveis
        foreach (var skill in availableSkills)
        {
            GameObject entry = Instantiate(availableSkillEntryPrefab, availableSkillsContainer);
            
            // Configura entry
            ConfigureAvailableSkillEntry(entry, skill);
        }
    }

    private void ConfigureLearnedSkillEntry(GameObject entry, LearnedSkillData skill)
    {
        var nameText = entry.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        var levelText = entry.transform.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
        var slotText = entry.transform.Find("SlotText")?.GetComponent<TextMeshProUGUI>();
        var button = entry.GetComponent<Button>();

        if (nameText != null)
            nameText.text = skill.template.name;

        if (levelText != null)
            levelText.text = $"Lv. {skill.currentLevel}/{skill.template.maxLevel}";

        if (slotText != null)
            slotText.text = skill.slotNumber > 0 ? $"Slot {skill.slotNumber}" : "Sem slot";

        if (button != null)
        {
            button.onClick.AddListener(() => SelectLearnedSkill(skill));
        }
    }

    private void ConfigureAvailableSkillEntry(GameObject entry, SkillTemplateData skill)
    {
        var nameText = entry.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        var reqText = entry.transform.Find("RequirementText")?.GetComponent<TextMeshProUGUI>();
        var button = entry.GetComponent<Button>();

        if (nameText != null)
            nameText.text = skill.name;

        if (reqText != null)
        {
            var charData = WorldManager.Instance.GetLocalCharacterData();
            bool canLearn = charData != null && charData.level >= skill.requiredLevel;
            
            string color = canLearn ? "lime" : "red";
            reqText.text = $"<color={color}>Requer Lv. {skill.requiredLevel}</color>";
        }

        if (button != null)
        {
            button.onClick.AddListener(() => SelectAvailableSkill(skill));
        }
    }

    /// <summary>
    /// Seleciona uma skill aprendida
    /// </summary>
    private void SelectLearnedSkill(LearnedSkillData skill)
    {
        selectedLearnedSkill = skill;
        selectedAvailableSkill = null;
        
        ShowSkillInfo(skill.template, skill);
    }

    /// <summary>
    /// Seleciona uma skill disponível
    /// </summary>
    private void SelectAvailableSkill(SkillTemplateData skill)
    {
        selectedAvailableSkill = skill;
        selectedLearnedSkill = null;
        
        ShowSkillInfo(skill, null);
    }

    /// <summary>
    /// Mostra informações detalhadas da skill
    /// </summary>
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

        // Configura botões
        UpdateInfoButtons(template, learnedData);
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

        // Nível atual
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
            bool showLearn = learnedData == null && selectedAvailableSkill != null;
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
        if (selectedAvailableSkill == null)
            return;

        // Mostra seleção de slot
        ShowSlotSelection();
    }

    private void OnLevelUpButtonClick()
    {
        if (selectedLearnedSkill == null)
            return;

        SkillManager.Instance?.LevelUpSkill(selectedLearnedSkill.skillId);
    }

    private void OnAssignSlotButtonClick()
    {
        if (selectedLearnedSkill == null)
            return;

        // Mostra seleção de slot
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
            
            GameObject buttonObj = new GameObject($"SlotButton_{i}");
            buttonObj.transform.SetParent(slotButtonsContainer);
            
            Button button = buttonObj.AddComponent<Button>();
            Image image = buttonObj.AddComponent<Image>();
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = i.ToString();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 24;
            
            button.onClick.AddListener(() => OnSlotSelected(slotNumber));
        }
    }

    private void OnSlotSelected(int slotNumber)
    {
        if (slotSelectionPanel != null)
            slotSelectionPanel.SetActive(false);

        // Aprender skill nova
        if (selectedAvailableSkill != null)
        {
            SkillManager.Instance?.LearnSkill(selectedAvailableSkill.id, slotNumber);
        }
        // Reatribuir slot de skill existente
        else if (selectedLearnedSkill != null)
        {
            // TODO: Implementar reatribuição de slot
            Debug.Log($"Reatribuir skill {selectedLearnedSkill.skillId} para slot {slotNumber}");
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