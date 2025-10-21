using UnityEngine;

/// <summary>
/// ScriptableObject que define uma skill
/// Coloque em: Assets/Scripts/Skills/SkillData.cs
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "MMO/Skill")]
public class SkillData : ScriptableObject
{
    [Header("Basic Info")]
    public int skillId;
    public string skillName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;

    [Header("Skill Type")]
    public SkillType skillType;
    public TargetType targetType;

    [Header("Costs & Cooldown")]
    public int manaCost;
    public int healthCost;
    public float cooldown;
    public float castTime;
    public float range;

    [Header("Visual Effects")]
    public GameObject castEffectPrefab;
    public GameObject projectilePrefab;
    public GameObject hitEffectPrefab;
    public GameObject aoeIndicatorPrefab;
    
    [Header("Animation")]
    public string animationTrigger = "CastSkill";
    public float animationDuration = 1f;

    [Header("Sound")]
    public AudioClip castSound;
    public AudioClip hitSound;

    [Header("AOE Settings (se aplicável)")]
    public float aoeRadius = 0f;
    public int maxTargets = -1;

    [Header("UI")]
    public Color skillColor = Color.white;
    public KeyCode defaultHotkey = KeyCode.Alpha1;
}

public enum SkillType
{
    Attack,     // Causa dano
    Heal,       // Cura
    Buff,       // Aumenta stats
    Debuff,     // Diminui stats
    Summon,     // Invoca algo
    Teleport    // Movimento especial
}

public enum TargetType
{
    Single,     // Único alvo (clique no monstro)
    Self,       // No próprio player
    Ground,     // Área no chão (AOE)
    Direction,  // Direção do mouse
    NoTarget    // Sem alvo (buff próprio)
}