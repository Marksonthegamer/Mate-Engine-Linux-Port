using System.Collections.Generic;
using UnityEngine;
using UniVRM10;
using VRM;

[DisallowMultipleComponent]
public class UniversalBlendshapes : MonoBehaviour
{
    [Header("Universal Preview")]
    [Range(0f, 1f)] public float Blink, Blink_L, Blink_R, LookUp, LookDown, LookLeft, LookRight, Neutral;
    [Range(0f, 1f)] public float A, I, U, E, O, Joy, Angry, Sorrow, Fun;
    public float fadeSpeed = 5f, safeTimeout = 2f, minHoldTime = 0.1f;

    private VRMBlendShapeProxy proxy0; private Vrm10Instance vrm1; private Vrm10RuntimeExpression expr1;
    private class BlendState { public float value, lastInput, lastUpdateTime, holdUntil; }

    private readonly Dictionary<string, BlendState> states = new();
    private readonly List<KeyValuePair<BlendShapeKey, float>> reusableList = new();

    private static readonly string[] keys = {
        "Blink", "Blink_L", "Blink_R",
        "LookUp", "LookDown", "LookLeft", "LookRight",
        "Neutral", "A", "I", "U", "E", "O",
        "Joy", "Angry", "Sorrow", "Fun"
    };

    private static readonly BlendShapePreset[] vrm0Presets = {
        BlendShapePreset.Blink, BlendShapePreset.Blink_L, BlendShapePreset.Blink_R,
        BlendShapePreset.LookUp, BlendShapePreset.LookDown, BlendShapePreset.LookLeft, BlendShapePreset.LookRight,
        BlendShapePreset.Neutral,
        BlendShapePreset.A, BlendShapePreset.I, BlendShapePreset.U, BlendShapePreset.E, BlendShapePreset.O,
        BlendShapePreset.Joy, BlendShapePreset.Angry, BlendShapePreset.Sorrow, BlendShapePreset.Fun
    };

    private static readonly Dictionary<string, string> vrm10KeyMap = new()
    {
        { "A", "aa" }, { "I", "ih" }, { "U", "ou" }, { "E", "ee" }, { "O", "oh" },
        { "Joy", "happy" }, { "Angry", "angry" }, { "Sorrow", "sad" }, { "Fun", "relaxed" },
        { "Blink", "blink" }, { "Blink_L", "blinkLeft" }, { "Blink_R", "blinkRight" },
        { "LookUp", "lookUp" }, { "LookDown", "lookDown" }, { "LookLeft", "lookLeft" }, { "LookRight", "lookRight" },
        { "Neutral", "neutral" }
    };

    private readonly Dictionary<string, ExpressionKey> vrm1ExpressionKeyMap = new();
    private readonly float[] valueCache = new float[keys.Length];

    private void Awake()
    {
        proxy0 = GetComponent<VRMBlendShapeProxy>();
        vrm1 = GetComponentInChildren<Vrm10Instance>(true);
        expr1 = vrm1 != null ? vrm1.Runtime?.Expression : null;

        for (int i = 0; i < keys.Length; i++)
            states[keys[i]] = new BlendState();

        if (expr1 != null)
        {
            vrm1ExpressionKeyMap.Clear();
            foreach (var k in expr1.ExpressionKeys)
            {
                if (!vrm1ExpressionKeyMap.ContainsKey(k.Name))
                    vrm1ExpressionKeyMap[k.Name] = k;
            }
        }
    }

    private void LateUpdate()
    {
        float now = Time.time;
        float dt = Time.deltaTime;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            float input = valueCache[i] = GetInputValue(i);
            UpdateState(key, input, now, dt);
        }

        if (proxy0 != null)
        {
            reusableList.Clear();
            for (int i = 0; i < keys.Length; i++)
            {
                reusableList.Add(new KeyValuePair<BlendShapeKey, float>(
                    BlendShapeKey.CreateFromPreset(vrm0Presets[i]), states[keys[i]].value
                ));
            }
            proxy0.SetValues(reusableList);
            proxy0.Apply();
        }
        else if (expr1 != null)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (!vrm10KeyMap.TryGetValue(key, out var mapped)) mapped = key;
                if (vrm1ExpressionKeyMap.TryGetValue(mapped, out var exprKey))
                {
                    expr1.SetWeight(exprKey, states[key].value);
                }
            }
        }
    }

    private float GetInputValue(int i) => i switch
    {
        0 => Blink,
        1 => Blink_L,
        2 => Blink_R,
        3 => LookUp,
        4 => LookDown,
        5 => LookLeft,
        6 => LookRight,
        7 => Neutral,
        8 => A,
        9 => I,
        10 => U,
        11 => E,
        12 => O,
        13 => Joy,
        14 => Angry,
        15 => Sorrow,
        16 => Fun,
        _ => 0f
    };


    private void UpdateState(string key, float input, float now, float dt)
    {
        if (!states.TryGetValue(key, out var state)) return;

        bool changed = !Mathf.Approximately(input, state.lastInput);
        bool activelyDriven = !Mathf.Approximately(input, 0f);

        if (changed || activelyDriven)
        {
            state.lastInput = input;
            state.lastUpdateTime = now;
            state.value = input;
            state.holdUntil = now + minHoldTime;
        }
        else
        {
            if (now < state.holdUntil)
            {
                state.value = input;
            }
            else
            {
                float idleTime = now - state.lastUpdateTime;
                if (idleTime > safeTimeout)
                {
                    state.value = 0f;
                }
                else
                {
                    state.value = Mathf.MoveTowards(state.value, 0f, fadeSpeed * dt);
                }
            }
        }
    }
}
