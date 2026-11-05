using UnityEngine;

namespace BetterChests;

/// <summary>
/// The look of an auto deposited item: a copy of it is thrown like a normal drop, lies on the ground for
/// a moment, then is pulled into the chest it went into. The copy is only for show and exists only on this
/// client; the item is already in the chest by the time it is thrown.
/// </summary>
internal sealed class DepositEffect : MonoBehaviour
{
    private const float FallTime = 0.5f;
    private const float FlyTime = 0.4f;
    private const float ArcHeight = 0.5f;
    private const float EndScale = 0.15f;
    private const float SpinSpeed = 720f;

    /// <summary>
    /// The chest model's pivot is at its base, so aim this far above it when it has no collider.
    /// </summary>
    private const float FallbackTargetHeight = 0.5f;

    private Container _container;
    private Vector3 _target;
    /// <summary>
    /// Not run if the effect is destroyed early, e.g. by logging out.
    /// </summary>
    private System.Action _onArrive;

    private Rigidbody _body;
    private Collider[] _colliders;
    private Vector3 _flyFrom;
    private Vector3 _startScale;
    private float _time;
    private bool _flying;

    /// <summary>
    /// Throws a copy of <paramref name="item"/> from where the player would drop it.
    /// <paramref name="onArrive"/> runs when it reaches the chest.
    /// </summary>
    public static void Spawn(Humanoid thrower, ItemDrop.ItemData item, Container container, System.Action onArrive)
    {
        GameObject prefab = item.m_dropPrefab;
        if (prefab == null)
        {
            onArrive();
            return;
        }

        Transform from = thrower.transform;
        Vector3 position = from.position + from.forward + from.up;

        // Created under an inactive object, so none of the prefab's scripts start (no network object, no
        // pickup) before they are removed.
        GameObject holder = new("BetterChests_DepositHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(prefab, position, from.rotation, holder.transform);
        go.name = "BetterChests_Deposit";

        // In reverse, so components are removed before the ones they depend on.
        MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = scripts.Length - 1; i >= 0; i--)
        {
            DestroyImmediate(scripts[i]);
        }

        go.transform.localScale = item.GetScale();
        go.transform.SetParent(null, true);
        Destroy(holder);

        DepositEffect effect = go.AddComponent<DepositEffect>();
        effect._container = container;
        effect._target = TargetPoint(container);
        effect._onArrive = onArrive;
        effect._colliders = go.GetComponentsInChildren<Collider>();
        effect._body = go.GetComponent<Rigidbody>();
        if (effect._body != null)
        {
            // Same throw as a vanilla drop.
            float speed = item.GetWeight() >= 300f ? 0.5f : 5f;
            effect._body.linearVelocity = (from.forward + Vector3.up) * speed;
        }
    }

    private static Vector3 TargetPoint(Container container)
    {
        Collider collider = container.GetComponentInChildren<Collider>();
        return collider != null
            ? collider.bounds.center
            : container.transform.position + (Vector3.up * FallbackTargetHeight);
    }

    private void Update()
    {
        _time += Time.deltaTime;

        if (!_flying)
        {
            if (_time >= FallTime)
            {
                StartFlying();
            }

            return;
        }

        // The chest may have been destroyed meanwhile; then keep heading for where it was.
        if (_container != null)
        {
            _target = TargetPoint(_container);
        }

        float t = Mathf.Clamp01((_time - FallTime) / FlyTime);
        // Slow at first, then faster, like being sucked in.
        float eased = t * t;
        transform.position = Vector3.Lerp(_flyFrom, _target, eased) + (Vector3.up * (Mathf.Sin(t * Mathf.PI) * ArcHeight));
        transform.localScale = _startScale * Mathf.Lerp(1f, EndScale, eased);
        transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);

        if (t >= 1f)
        {
            Arrive();
        }
    }

    private void StartFlying()
    {
        _flying = true;
        _flyFrom = transform.position;
        _startScale = transform.localScale;

        if (_body != null)
        {
            _body.isKinematic = true;
        }

        foreach (Collider collider in _colliders)
        {
            collider.enabled = false;
        }
    }

    private void Arrive()
    {
        Destroy(gameObject);
        _onArrive();
    }
}
