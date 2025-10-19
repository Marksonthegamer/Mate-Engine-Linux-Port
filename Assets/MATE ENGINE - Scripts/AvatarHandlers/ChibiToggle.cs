using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ChibiToggle : MonoBehaviour
{
    [Header("Chibi Scale Settings")]
    public Vector3 chibiArmatureScale = new Vector3(0.3f, 0.3f, 0.3f);
    public Vector3 chibiHeadScale = new Vector3(2.7f, 2.7f, 2.7f);
    public Vector3 chibiUpperLegScale = new Vector3(0.6f, 0.6f, 0.6f);

    [Header("Sound Effects")]
    public AudioSource audioSource;
    public List<AudioClip> chibiEnterSounds = new List<AudioClip>();
    public List<AudioClip> chibiExitSounds = new List<AudioClip>();

    [Header("Particle Effect")]
    public GameObject particleEffectObject;
    public float particleDuration = 4f;

    private Animator anim;
    private Transform armatureRoot, head, leftFoot, rightFoot;
    private Transform leftUpperLeg, rightUpperLeg;

    private bool isChibi;
    private Vector3 originalArmaturePosition;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.applyRootMotion = false;

        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        head = anim.GetBoneTransform(HumanBodyBones.Head);
        leftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        leftUpperLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);

        if (hips != null)
        {
            armatureRoot = hips;
            while (armatureRoot.parent != null && armatureRoot.parent != transform)
                armatureRoot = armatureRoot.parent;

            originalArmaturePosition = armatureRoot.localPosition;
        }
    }

    public void ToggleChibiMode()
    {
        if (!armatureRoot || !head || !leftFoot || !rightFoot) return;

        bool becomingChibi = !isChibi;

        float originalFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);

        armatureRoot.localScale = becomingChibi ? chibiArmatureScale : Vector3.one;
        head.localScale = becomingChibi ? chibiHeadScale : Vector3.one;
        if (leftUpperLeg) leftUpperLeg.localScale = becomingChibi ? chibiUpperLegScale : Vector3.one;
        if (rightUpperLeg) rightUpperLeg.localScale = becomingChibi ? chibiUpperLegScale : Vector3.one;

        isChibi = becomingChibi;
        PlayRandomSound(becomingChibi);
        TriggerParticles();
        StartCoroutine(AdjustFeetToGround(originalFootY));
    }

    private IEnumerator AdjustFeetToGround(float originalFootY)
    {
        yield return null;

        float newFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        float offsetY = originalFootY - newFootY;
        transform.position += new Vector3(0f, offsetY, 0f);
    }

    void PlayRandomSound(bool enteringChibi)
    {
        if (!audioSource) return;

        List<AudioClip> sourceList = enteringChibi ? chibiEnterSounds : chibiExitSounds;
        if (sourceList.Count == 0) return;

        AudioClip clip = sourceList[Random.Range(0, sourceList.Count)];
        audioSource.PlayOneShot(clip);
    }

    void TriggerParticles()
    {
        if (!particleEffectObject) return;

        StopAllCoroutines();
        StartCoroutine(TemporaryParticleCoroutine());
    }

    IEnumerator TemporaryParticleCoroutine()
    {
        particleEffectObject.SetActive(true);
        yield return new WaitForSeconds(particleDuration);
        particleEffectObject.SetActive(false);
    }
}
