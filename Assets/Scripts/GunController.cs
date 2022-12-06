using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
  [Header("Functional Options")]
  [SerializeField] private bool CanShoot = true;

  [Header("Controls")]
  [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
  [SerializeField] private KeyCode reloadKey = KeyCode.R;

  [Header("Gun Parameters")]
  [SerializeField] private float dmgHead = 75f;
  [SerializeField] private float dmgBody = 36f;
  [SerializeField] private float fireRate = .25f;
  [SerializeField] private int clipSize = 12;
  [SerializeField] private int reservedAmmoCapacity = 48;
  [SerializeField] private AudioSource gunAudioSource = default;
  [SerializeField] private AudioClip shootAudioClip = default;
  [SerializeField] private AudioClip noAmmoAudioClip = default;

  private int currentAmmoInClip;
  private int ammoInReserve;

  [Header("Muzzle Parameters")]
  [SerializeField] private ParticleSystem muzzleFlash;

  [Header("Aim Parameters")]
  [SerializeField] private Vector3 normalLocalPosition;
  [SerializeField] private Vector3 aimingLocalPosition;
  [SerializeField] private float aimSmooth = 10f;

  [Header("Weapon Sway")]
  [SerializeField] private float weaponSwayAmount = -.1f;

  [Header("Weapon Recoil")]
  [SerializeField] private float fireRecoilForward = .1f;
  [SerializeField] private bool randomizeRecoil;
  //Just have pattern if randomizeRecoil is off
  //[SerializeField] private Vector2[] recoilPattern;
  [SerializeField] private Vector2 randomReacoilConstraints = Vector2.zero;
  //Need to publish movement variables
  //[SerializeField] private Vector2 walkRipRecoilConstraints = new Vector2(2f, 3.2f);
  //[SerializeField] private Vector2 sprintRipRecoilConstraints = new Vector2(4f, 6.7f);
  //[SerializeField] private Vector2 crouchRipRecoilConstraints = new Vector2(1f, 1f);
  //[SerializeField] private Vector2 walkAimRecoilConstraints = new Vector2(1.25f, 1);
  //[SerializeField] private Vector2 sprintAimRecoilConstraints = new Vector2(3.1f, 3.3f);
  //[SerializeField] private Vector2 crouchAimRecoilConstraints = new Vector2(.25f, .25f);

  public static Action<float, float> OnFire;
  public static Action<float, float> OnReload;

  private Vector2 mouseAxis = Vector2.zero;
  private Vector2 recoil = Vector2.zero;

  private bool isReloading = false;

  private FirstPersonController fpsController;
  private Animator gunAnimator;

  private void Start()
  {
    fpsController = GetComponentInParent<FirstPersonController>();
    gunAnimator = GetComponent<Animator>();

    currentAmmoInClip = clipSize;
    ammoInReserve = reservedAmmoCapacity;
  }

  private void Update()
  {
    HandleAim();

    WeaponSway();

    if (Input.GetKey(fireKey) && CanShoot && currentAmmoInClip > 0 && !isReloading)
    {
      CanShoot = false;
      currentAmmoInClip--;
      StartCoroutine(ShootGun());
    }
    else if (Input.GetKeyDown(reloadKey) && currentAmmoInClip < clipSize && ammoInReserve > 0 && !isReloading)
    {
      StartCoroutine(ReloadGun());
    }
    else if (Input.GetKeyDown(fireKey) && CanShoot && currentAmmoInClip <= 0)
    {
      gunAudioSource.PlayOneShot(noAmmoAudioClip);
    }
  }

  private void WeaponSway()
  {
    mouseAxis = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
    mouseAxis *= 2f;
    transform.localPosition += (Vector3)mouseAxis * weaponSwayAmount / 1000f;
  }

  private void HandleAim()
  {
    Vector3 target = normalLocalPosition;
    if (fpsController.isZooming)
      target = aimingLocalPosition;

    Vector3 desiredPosition = Vector3.Lerp(transform.localPosition, target, Time.deltaTime * aimSmooth);

    transform.localPosition = desiredPosition;
  }

  private void HandleRecoil()
  {
    transform.localPosition -= Vector3.forward * fireRecoilForward;

    if (randomizeRecoil)
    {
      float xRecoil = UnityEngine.Random.Range(-randomReacoilConstraints.x, randomReacoilConstraints.x);
      float yRecoil = UnityEngine.Random.Range(-randomReacoilConstraints.y, randomReacoilConstraints.y);

      recoil = new Vector2(xRecoil, yRecoil);

      fpsController.currentRotation += recoil;
    }
    //Recoil Pattern
    //else
    //{
    //    int currentStep = clipSize + 1 - currentAmmoInClip;
    //    currentStep = Mathf.Clamp(currentStep, 0, recoilPattern.Length - 1);

    //    fpsController.currentRotation += recoilPattern[currentStep];
    //}
  }

  private void RaycastForEnemy()
  {
    if (Physics.Raycast(fpsController.playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
    {
      if (hit.collider.GetComponentInParent<Enemy>())
      {
        float dmg = hit.collider.tag == "Hit/Head" ? dmgHead : dmgBody;
        hit.collider.GetComponentInParent<Enemy>().ApplyDamage(dmg);
      }
    }
  }

  private IEnumerator ShootGun()
  {
    HandleRecoil();
    muzzleFlash.Play();
    gunAudioSource.PlayOneShot(shootAudioClip);
    OnFire?.Invoke(currentAmmoInClip, ammoInReserve);

    RaycastForEnemy();

    yield return new WaitForSeconds(fireRate);
    CanShoot = true;
  }

  private IEnumerator ReloadGun()
  {
    CanShoot = false;
    isReloading = true;
    gunAnimator.SetTrigger("Reloading");

    yield return new WaitForSeconds(2.5f);
    int amountNeeded = clipSize - currentAmmoInClip;
    if (amountNeeded >= ammoInReserve)
    {
      currentAmmoInClip += ammoInReserve;
      ammoInReserve -= amountNeeded;
    }
    else
    {
      currentAmmoInClip = clipSize;
      ammoInReserve -= amountNeeded;
    }
    if (ammoInReserve < 0)
      ammoInReserve = 0;

    OnReload?.Invoke(currentAmmoInClip, ammoInReserve);
    isReloading = false;
    CanShoot = true;
  }
}
