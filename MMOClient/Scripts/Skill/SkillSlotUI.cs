using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Componente de UI para um slot de skill na hotbar
/// Coloque em: Assets/Scripts/Skills/UI/SkillSlotUI.cs
/// </summary>
public class SkillSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI Elements")]
    public Image skillIcon;
    public Image cooldownOverlay;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI hotkeyText;
    public GameObject emptyIndicator;

    [Header("Settings")]
    public int slotIndex;
    public KeyCode hotkey;

    [HideInInspector]
    public SkillData skillData;

    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 originalPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        // Define hotkey text
        if (hotkeyText != null)
        {
            hotkeyText.text = (slotIndex + 1).ToString();
        }

        UpdateVisuals();
    }

    private void Update()
    {
        // Verifica hotkey
        if (Input.GetKeyDown(hotkey) || Input.GetKeyDown(KeyCode.Alpha1 + slotIndex))
        {
            UseSkill();
        }
    }

    // ==================== SKILL ASSIGNMENT ====================

    public void SetSkill(SkillData skill)
    {
        skillData = skill;
        UpdateVisuals();
    }

    public void ClearSlot()
    {
        skillData = null;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        bool hasSkill = skillData != null;

        if (skillIcon != null)
        {
            skillIcon.enabled = hasSkill;
            
            if (hasSkill && skillData.icon != null)
            {
                skillIcon.sprite = skillData.icon;
                skillIcon.color = Color.white;
            }
        }

        if (emptyIndicator != null)
        {
            emptyIndicator.SetActive(!hasSkill);
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 0f;
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(false);
        }
    }

    // ==================== COOLDOWN ====================

    public void UpdateCooldown(float remaining)
    {
        if (skillData == null)
            return;

        bool onCooldown = remaining > 0f;

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(onCooldown);
            
            if (onCooldown)
            {
                float percent = remaining / skillData.cooldown;
                cooldownOverlay.fillAmount = percent;
            }
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(onCooldown);
            
            if (onCooldown)
            {
                cooldownText.text = remaining < 10f ? $"{remaining:F1}" : $"{Mathf.CeilToInt(remaining)}";
            }
        }
    }

    // ==================== INTERACTIONS ====================

    private void UseSkill()
    {
        if (skillData != null && SkillManager.Instance != null)
        {
            SkillManager.Instance.UseSkillFromHotbar(slotIndex);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            UseSkill();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Right-click para remover
            if (skillData != null)
            {
                ShowContextMenu();
            }
        }
    }

    private void ShowContextMenu()
    {
        // Implementar menu de contexto se necessário
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.ClearHotbarSlot(slotIndex);
        }
    }

    // ==================== DRAG & DROP ====================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (skillData == null)
            return;

        originalPosition = rectTransform.anchoredPosition;
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (skillData == null)
            return;

        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (skillData == null)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        rectTransform.anchoredPosition = originalPosition;

        // Verifica se dropou em outro slot
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            var targetSlot = result.gameObject.GetComponent<SkillSlotUI>();
            
            if (targetSlot != null && targetSlot != this)
            {
                SwapSkills(targetSlot);
                break;
            }
        }
    }

    private void SwapSkills(SkillSlotUI otherSlot)
    {
        var tempSkill = skillData;
        SetSkill(otherSlot.skillData);
        otherSlot.SetSkill(tempSkill);

        // Salva hotbar
        if (SkillManager.Instance != null)
        {
            // Trigger save no SkillManager
            Debug.Log($"Swapped skills between slot {slotIndex} and {otherSlot.slotIndex}");
        }
    }
}