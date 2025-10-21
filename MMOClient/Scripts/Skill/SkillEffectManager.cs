using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Gerencia todos os efeitos visuais de skills
/// Coloque em: Assets/Scripts/Skills/SkillEffectManager.cs
/// </summary>
public class SkillEffectManager : MonoBehaviour
{
    public static SkillEffectManager Instance { get; private set; }

    [Header("AOE Indicator")]
    public GameObject aoeIndicatorPrefab;
    public Material aoeIndicatorMaterial;
    public Color aoeColorValid = new Color(0f, 1f, 0f, 0.3f);
    public Color aoeColorInvalid = new Color(1f, 0f, 0f, 0.3f);

    [Header("Default Effects")]
    public GameObject defaultCastEffect;
    public GameObject defaultHitEffect;
    public GameObject defaultProjectile;

    [Header("Pooling")]
    public int effectPoolSize = 20;

    private Dictionary<string, Queue<GameObject>> effectPools = new Dictionary<string, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeEffectPools();
    }

    // ==================== POOLING ====================

    private void InitializeEffectPools()
    {
        if (defaultCastEffect != null)
        {
            CreatePool("cast_default", defaultCastEffect, effectPoolSize);
        }

        if (defaultHitEffect != null)
        {
            CreatePool("hit_default", defaultHitEffect, effectPoolSize);
        }

        if (defaultProjectile != null)
        {
            CreatePool("projectile_default", defaultProjectile, effectPoolSize);
        }
    }

    private void CreatePool(string poolName, GameObject prefab, int size)
    {
        if (effectPools.ContainsKey(poolName))
            return;

        var pool = new Queue<GameObject>();

        for (int i = 0; i < size; i++)
        {
            var obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }

        effectPools[poolName] = pool;
    }

    private GameObject GetFromPool(string poolName)
    {
        if (!effectPools.ContainsKey(poolName) || effectPools[poolName].Count == 0)
            return null;

        var obj = effectPools[poolName].Dequeue();
        obj.SetActive(true);
        return obj;
    }

    private void ReturnToPool(string poolName, GameObject obj)
    {
        if (!effectPools.ContainsKey(poolName))
            return;

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        effectPools[poolName].Enqueue(obj);
    }

    // ==================== AOE INDICATOR ====================

    public GameObject CreateAOEIndicator(float radius)
    {
        GameObject indicator;

        if (aoeIndicatorPrefab != null)
        {
            indicator = Instantiate(aoeIndicatorPrefab);
        }
        else
        {
            // Cria indicador procedural
            indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(indicator.GetComponent<Collider>());
            
            indicator.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            
            var renderer = indicator.GetComponent<Renderer>();
            
            if (aoeIndicatorMaterial != null)
            {
                renderer.material = aoeIndicatorMaterial;
            }
            else
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = aoeColorValid;
                renderer.material = mat;
            }
        }

        indicator.name = "AOE_Indicator";
        return indicator;
    }

    public void UpdateAOEIndicator(GameObject indicator, Vector3 position, bool isValid)
    {
        if (indicator == null)
            return;

        indicator.transform.position = position;

        var renderer = indicator.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.color = isValid ? aoeColorValid : aoeColorInvalid;
        }
    }

    // ==================== CAST EFFECTS ====================

    public void PlayCastEffect(SkillData skill, Vector3 position, Transform parent = null)
    {
        GameObject effectPrefab = skill.castEffectPrefab ?? defaultCastEffect;

        if (effectPrefab == null)
            return;

        var effect = GetFromPool("cast_" + skill.skillId) ?? Instantiate(effectPrefab);
        
        effect.transform.position = position;
        
        if (parent != null)
        {
            effect.transform.SetParent(parent);
        }

        StartCoroutine(AutoDestroyEffect(effect, skill.castTime, "cast_" + skill.skillId));
    }

    // ==================== HIT EFFECTS ====================

    public void PlayHitEffect(SkillData skill, Vector3 position)
    {
        GameObject effectPrefab = skill.hitEffectPrefab ?? defaultHitEffect;

        if (effectPrefab == null)
            return;

        var effect = GetFromPool("hit_" + skill.skillId) ?? Instantiate(effectPrefab);
        effect.transform.position = position;

        StartCoroutine(AutoDestroyEffect(effect, 2f, "hit_" + skill.skillId));
    }

    // ==================== PROJECTILE ====================

    public void LaunchProjectile(SkillData skill, Vector3 startPos, Vector3 targetPos, System.Action onHit = null)
    {
        GameObject projectilePrefab = skill.projectilePrefab ?? defaultProjectile;

        if (projectilePrefab == null)
        {
            // Se não tem projétil, hit instantâneo
            onHit?.Invoke();
            PlayHitEffect(skill, targetPos);
            return;
        }

        var projectile = Instantiate(projectilePrefab, startPos, Quaternion.identity);
        
        // Rotaciona em direção ao alvo
        Vector3 direction = (targetPos - startPos).normalized;
        projectile.transform.rotation = Quaternion.LookRotation(direction);

        // Inicia movimento
        StartCoroutine(MoveProjectile(projectile, startPos, targetPos, 15f, () =>
        {
            onHit?.Invoke();
            PlayHitEffect(skill, targetPos);
            Destroy(projectile);
        }));
    }

    private IEnumerator MoveProjectile(GameObject projectile, Vector3 start, Vector3 end, float speed, System.Action onComplete)
    {
        float distance = Vector3.Distance(start, end);
        float duration = distance / speed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            projectile.transform.position = Vector3.Lerp(start, end, t);
            
            yield return null;
        }

        projectile.transform.position = end;
        onComplete?.Invoke();
    }

    // ==================== SKILL VISUALS ====================

    public void PlaySkillVisuals(SkillData skill, Vector3 casterPos, object target)
    {
        // Cast effect
        PlayCastEffect(skill, casterPos);

        if (target is int monsterId)
        {
            // Skill de alvo único
            var monster = FindMonsterById(monsterId);
            
            if (monster != null)
            {
                Vector3 targetPos = monster.transform.position + Vector3.up * 1.5f;

                if (skill.projectilePrefab != null || skill.skillType == SkillType.Attack)
                {
                    LaunchProjectile(skill, casterPos + Vector3.up * 1.5f, targetPos);
                }
                else
                {
                    PlayHitEffect(skill, targetPos);
                }
            }
        }
        else if (target is Vector3 groundPos)
        {
            // Skill de área
            PlayAOEEffect(skill, groundPos);
        }
    }

    private void PlayAOEEffect(SkillData skill, Vector3 position)
    {
        GameObject effectPrefab = skill.hitEffectPrefab ?? defaultHitEffect;

        if (effectPrefab == null)
            return;

        var effect = Instantiate(effectPrefab, position, Quaternion.identity);
        
        // Escala baseado no raio
        if (skill.aoeRadius > 0f)
        {
            effect.transform.localScale = Vector3.one * skill.aoeRadius;
        }

        Destroy(effect, 3f);
    }

    // ==================== UTILITY ====================

    private IEnumerator AutoDestroyEffect(GameObject effect, float delay, string poolName = null)
    {
        yield return new WaitForSeconds(delay);

        if (poolName != null && effectPools.ContainsKey(poolName))
        {
            ReturnToPool(poolName, effect);
        }
        else if (effect != null)
        {
            Destroy(effect);
        }
    }

    private MonsterController FindMonsterById(int monsterId)
    {
        var monsters = FindObjectsOfType<MonsterController>();
        
        foreach (var monster in monsters)
        {
            if (monster.monsterId == monsterId)
            {
                return monster;
            }
        }

        return null;
    }

    // ==================== DAMAGE NUMBERS ====================

    public void ShowDamageNumber(Vector3 position, int damage, bool isCritical)
    {
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowDamage(position, damage, isCritical);
        }
    }

    public void ShowHealNumber(Vector3 position, int heal)
    {
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowHeal(position, heal);
        }
    }
}