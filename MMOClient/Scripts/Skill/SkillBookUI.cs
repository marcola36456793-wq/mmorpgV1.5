using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Interface do livro de skills (tecla K)
/// Coloque em: Assets/Scripts/Skills/UI/SkillBookUI.cs
/// </summary>
public class SkillBookUI : MonoBehaviour
{
    public static SkillBookUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject skillBookPanel;
    public GameObject learnedSkillsPanel;
    public GameObject availableSkillsPanel;

    [Header("Skill List")]
    public Transform learnedSkillsContainer;
    public Transform availableSkillsContainer;
    public GameObject skillEntryPrefab;

    [Header("Skill Details")]
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillDescriptionText;
    public TextMeshProUGUI skillStatsText;
    public Image skillIconImage;
    public Button learnButton;
    public Button assignToHotbarButton;

    [Header("Tabs")]
    public Button learnedTabButton;
    public Button availableTabButton;

    [Header("Close")]
    public Button closeButton;

    private bool isVisible = false;
    private SkillData selectedSkill;
    private List<GameObject> skillEntries = new List<GameObject>();

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
        // Setup buttons
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (learnButton != null)
            learnButton.onClick.AddListener(OnLearnButtonClick);

        if (assignToHotbarButton != null)
            assignToHotbarButton.onClick.AddListener(OnAssignToHotbarClick);

        if (learnedTabButton != null)
            learnedTabButton.onClick.AddListener(() => ShowTab(true));

        if (availableTabButton != null)
            availableTabButton.onClick.AddListener(() => ShowTab(false));

        Hide();
    }

    private void Update()
    {
        // Tecla K para abrir/fechar
        if (Input.GetKeyDown(KeyCode.K))
        {
            Toggle();
        }
    }

    // ==================== SHOW/HIDE ====================

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

        // Carrega skills do servidor
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.RequestSkillsFromServer();
        }

        RefreshSkillList();
        ShowTab(true); // Mostra learned skills por padrão
    }

    public void Hide()
    {
        if (skillBookPanel != null)
            skillBookPanel.SetActive(false);

        isVisible = false;
        selectedSkill = null;
    }

    // ==================== TABS ====================

    private void ShowTab(bool showLearned)
    {
        if (learnedSkillsPanel != null)
            learnedSkillsPanel.SetActive(showLearned);

        if (availableSkillsPanel != null)
            availableSkillsPanel.SetActive(!showLearned);

        // Atualiza visual dos botões
        if (learnedTabButton != null)
        {
            var colors = learnedTabButton.colors;
            colors.normalColor = showLearned ? Color.white : Color.gray;
            learnedTabButton.colors = colors;
        }

        if (availableTabButton != null)
        {
            var colors = availableTabButton.colors;
            colors.normalColor = !showLearned ? Color.white : Color.gray;
            availableTabButton.colors = colors;
        }

        if (showLearned)
        {
            ShowLearnedSkills();
        }
        else
        {
            ShowAvailableSkills();
        }
    }

    // ==================== SKILL LIST ====================

    public void RefreshSkillList()
    {
        ClearSkillEntries();

        if (learnedSkillsPanel != null && learnedSkillsPanel.activeSelf)
        {
            ShowLearnedSkills();
        }

        if (availableSkillsPanel != null && availableSkillsPanel.activeSelf)
        {
            ShowAvailableSkills();
        }
    }

    private void ShowLearnedSkills()
    {
        ClearContainer(learnedSkillsContainer);

        if (SkillManager.Instance == null)
            return;

        var learnedSkills = SkillManager.Instance.GetLearnedSkills();

        foreach (var skill in learnedSkills)
        {
            CreateSkillEntry(skill, learnedSkillsContainer, true);
        }

        if (learnedSkills.Count == 0)
        {
            CreateEmptyMessage(learnedSkillsContainer, "Você ainda não aprendeu nenhuma skill.\nVisite um NPC treinador!");
        }
    }

    private void ShowAvailableSkills()
    {
        // Por enquanto, mostra skills disponíveis localmente
        // Idealmente, pedir ao servidor
        ClearContainer(availableSkillsContainer);

        CreateEmptyMessage(availableSkillsContainer, "Visite um NPC treinador para aprender novas skills!");
    }

    private void CreateSkillEntry(SkillData skill, Transform container, bool isLearned)
    {
        if (skillEntryPrefab == null || container == null)
            return;

        var entry = Instantiate(skillEntryPrefab, container);
        skillEntries.Add(entry);

        // Preenche dados
        var nameText = entry.transform.Find("SkillName")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = skill.skillName;

        var iconImage = entry.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null && skill.icon != null)
            iconImage.sprite = skill.icon;

        var levelText = entry.transform.Find("Level")?.GetComponent<TextMeshProUGUI>();
        if (levelText != null)
            levelText.text = $"Lv. 1"; // TODO: Pegar level real da skill

        // Botão para selecionar
        var button = entry.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => SelectSkill(skill, isLearned));
        }
    }

    private void CreateEmptyMessage(Transform container, string message)
    {
        if (container == null)
            return;

        var messageObj = new GameObject("EmptyMessage");
        messageObj.transform.SetParent(container);

        var textComp = messageObj.AddComponent<TextMeshProUGUI>();
        textComp.text = message;
        textComp.fontSize = 18;
        textComp.color = Color.gray;
        textComp.alignment = TextAlignmentOptions.Center;

        var rectTransform = messageObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(400, 100);

        skillEntries.Add(messageObj);
    }

    private void ClearContainer(Transform container)
    {
        if (container == null)
            return;

        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }

    private void ClearSkillEntries()
    {
        foreach (var entry in skillEntries)
        {
            if (entry != null)
                Destroy(entry);
        }

        skillEntries.Clear();
    }

    // ==================== SKILL SELECTION ====================

    private void SelectSkill(SkillData skill, bool isLearned)
    {
        selectedSkill = skill;
        ShowSkillDetails(skill, isLearned);
    }

    private void ShowSkillDetails(SkillData skill, bool isLearned)
    {
        if (skillNameText != null)
            skillNameText.text = skill.skillName;

        if (skillDescriptionText != null)
            skillDescriptionText.text = skill.description;

        if (skillIconImage != null && skill.icon != null)
            skillIconImage.sprite = skill.icon;

        if (skillStatsText != null)
        {
            string stats = $"<b>Tipo:</b> {TranslateSkillType(skill.skillType)}\n";
            stats += $"<b>Alvo:</b> {TranslateTargetType(skill.targetType)}\n\n";
            
            if (skill.manaCost > 0)
                stats += $"<color=cyan>Mana:</color> {skill.manaCost}\n";
            
            if (skill.healthCost > 0)
                stats += $"<color=red>HP:</color> {skill.healthCost}\n";
            
            stats += $"<color=yellow>Cooldown:</color> {skill.cooldown}s\n";
            
            if (skill.castTime > 0)
                stats += $"<color=orange>Cast Time:</color> {skill.castTime}s\n";
            
            stats += $"<color=lime>Alcance:</color> {skill.range}m\n";
            
            if (skill.aoeRadius > 0)
                stats += $"<color=purple>Raio AOE:</color> {skill.aoeRadius}m\n";

            skillStatsText.text = stats;
        }

        // Botões
        if (learnButton != null)
            learnButton.gameObject.SetActive(!isLearned);

        if (assignToHotbarButton != null)
            assignToHotbarButton.gameObject.SetActive(isLearned);
    }

    // ==================== BUTTONS ====================

    private void OnLearnButtonClick()
    {
        if (selectedSkill == null)
        {
            ShowMessage("Selecione uma skill primeiro!");
            return;
        }

        // Envia request ao servidor
        var message = new
        {
            type = "learnSkill",
            skillId = selectedSkill.skillId
        };

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);

        Debug.Log($"📚 Requesting to learn: {selectedSkill.skillName}");
    }

    private void OnAssignToHotbarClick()
    {
        if (selectedSkill == null)
        {
            ShowMessage("Selecione uma skill primeiro!");
            return;
        }

        // Abre diálogo para escolher slot
        ShowHotbarSlotSelector();
    }

    private void ShowHotbarSlotSelector()
    {
        // Implementar diálogo de seleção de slot
        // Por enquanto, usa primeiro slot vazio
        if (SkillManager.Instance != null)
        {
            for (int i = 0; i < 9; i++)
            {
                if (SkillManager.Instance.hotbarSlots[i].skillData == null)
                {
                    SkillManager.Instance.AssignSkillToHotbar(selectedSkill, i);
                    ShowMessage($"{selectedSkill.skillName} atribuída ao slot {i + 1}");
                    return;
                }
            }

            ShowMessage("Hotbar cheia! Remova uma skill primeiro.");
        }
    }

    // ==================== UTILITY ====================

    private string TranslateSkillType(SkillType type)
    {
        return type switch
        {
            SkillType.Attack => "Ataque",
            SkillType.Heal => "Cura",
            SkillType.Buff => "Buff",
            SkillType.Debuff => "Debuff",
            SkillType.Summon => "Invocação",
            SkillType.Teleport => "Teleporte",
            _ => type.ToString()
        };
    }

    private string TranslateTargetType(TargetType type)
    {
        return type switch
        {
            TargetType.Single => "Alvo Único",
            TargetType.Self => "Próprio",
            TargetType.Ground => "Área (Chão)",
            TargetType.Direction => "Direção",
            TargetType.NoTarget => "Sem Alvo",
            _ => type.ToString()
        };
    }

    private void ShowMessage(string message)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog(message);
        }
    }

    private void OnDestroy()
    {
        ClearSkillEntries();
    }
}