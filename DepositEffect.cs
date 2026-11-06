using UnityEngine;

namespace BetterChests;

/// <summary>
/// The look of an auto deposited item: a copy of it is pulled from where the item lay into the chest it
/// went into. The copy is only for show and exists only on this client; the item is already in the chest
/// by the time it starts moving.
/// </summary>
internal sealed class DepositEffect : MonoBehaviour
{
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

    private Vector3 _flyFrom;
    private Vector3 _startScale;
    private float _time;

    /// <summary>
    /// Pulls a copy of <paramref name="item"/> from <paramref name="position"/> into the chest.
    /// <paramref name="onArrive"/> runs when it gets there.
    /// </summary>
    public static void Spawn(ItemDrop.ItemData item, Vector3 position, Quaternion rotation, Container container, System.Action onArrive)
    {
        GameObject prefab = item.m_dropPrefab;
        if (prefab == null)
        {
            onArrive();
            return;
        }

        // Created under an inactive object, so none of the prefab's scripts start (no network object, no
        // pickup) before they are removed.
        GameObject holder = new("BetterChests_DepositHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(prefab, position, rotation, holder.transform);
        go.name = "BetterChests_Deposit";

        // In reverse, so components are removed before the ones they depend on.
        MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = scripts.Length - 1; i >= 0; i--)
        {
            DestroyImmediate(scripts[i]);
        }

        // Moved by hand, so physics must leave it alone.
        foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        Rigidbody body = go.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
        }

        go.transform.localScale = item.GetScale();
        go.transform.SetParent(null, true);
        Destroy(holder);

        DepositEffect effect = go.AddComponent<DepositEffect>();
        effect._container = container;
        effect._target = TargetPoint(container);
        effect._onArrive = onArrive;
        effect._flyFrom = position;
        effect._startScale = go.transform.localScale;
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

        // The chest may have been destroyed meanwhile; then keep heading for where it was.
        if (_container != null)
        {
            _target = TargetPoint(_container);
        }

        float t = Mathf.Clamp01(_time / FlyTime);
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

    private void Arrive()
    {
        Destroy(gameObject);
        _onArrive();
    }
}
