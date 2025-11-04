using UnityEngine;
using System.Collections.Generic;

public class SolderBlob : MonoBehaviour
{
    public static GameObject prefab; // assign via inspector or static init
    static List<SolderBlob> blobs = new List<SolderBlob>();

    float accumulatedMs = 0f;
    float tipTempC = 350f;
    Renderer rend;
    float amount = 0f; // 0..1

    void Awake()
    {
        rend = GetComponent<Renderer>();
        blobs.Add(this);
    }

    public static void Spawn(Vector3 pos, float tipTemp)
    {
        if (prefab == null)
        {
            Debug.LogError("SolderBlob prefab not set");
            return;
        }
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        var sb = go.GetComponent<SolderBlob>();
        sb.tipTempC = tipTemp;
    }

    public static void UpdateAt(Vector3 pos, int contactMs, float tipTemp)
    {
        // find nearest blob within small radius, else spawn
        SolderBlob nearest = null;
        float minDist = 0.01f;
        foreach (var b in blobs)
        {
            float d = Vector3.Distance(b.transform.position, pos);
            if (d < minDist) { minDist = d; nearest = b; }
        }
        if (nearest == null)
        {
            Spawn(pos, tipTemp);
            return;
        }
        nearest.ReceiveContact(contactMs, tipTemp);
    }

    public static void EndAt(Vector3 pos, int contactMs, float tipTemp)
    {
        UpdateAt(pos, contactMs, tipTemp);
        // optionally mark finished
    }

    void ReceiveContact(int contactMs, float tipTemp)
    {
        accumulatedMs = contactMs;
        tipTempC = tipTemp;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        // amount model: amount = 1 - exp(-alpha * t)
        float alpha = 0.01f; // tune per experiment
        amount = 1.0f - Mathf.Exp(-alpha * accumulatedMs);
        // scale blob by amount
        transform.localScale = Vector3.one * (0.002f + 0.008f * amount); // tune scale
        // temperature visual: effective temp
        float T_ambient = 25f;
        float k = 0.005f;
        float T_eff = T_ambient + (tipTempC - T_ambient) * (1 - Mathf.Exp(-k * accumulatedMs));
        // map temperature to color (simple)
        Color col = TemperatureToColor(T_eff);
        rend.material.SetColor("_BaseColor", col);
        rend.material.SetFloat("_EmissionStrength", Mathf.Clamp01((T_eff - 100f) / 300f));
        // optionally set smoothness/metallic
        rend.material.SetFloat("_Smoothness", Mathf.Lerp(0.2f, 0.9f, amount));
    }

    Color TemperatureToColor(float T)
    {
        // very simple: <200 -> dark red, 200-400 -> orange-yellow, >400 -> white
        if (T < 200) return Color.Lerp(new Color(0.2f, 0.05f, 0.0f), Color.red, (T - 25) / 175f);
        if (T < 400) return Color.Lerp(Color.red, Color.yellow, (T - 200) / 200f);
        return Color.Lerp(Color.yellow, Color.white, (T - 400) / 200f);
    }

    void OnDestroy()
    {
        blobs.Remove(this);
    }
}
