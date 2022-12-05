using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GunController : MonoBehaviour
{
    [Header("Functional Options")]
    [SerializeField] private bool CanShoot = true;

    [Header("Controls")]
    [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    [Header("Gun Parameters")]
    [SerializeField] private float fireRate = .25f;
    [SerializeField] private int clipSize = 12;
    [SerializeField] private int reservedAmmoCapacity = 48;
    [SerializeField] private AudioSource gunAudioSource = default;
    [SerializeField] private AudioClip shootAudioClip = default;

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
    [SerializeField] private Vector2 randomReacoilConstraints;
    //Just have pattern if randomizeRecoil is off
    [SerializeField] private Vector2[] recoilPattern;

    private Vector2 mouseAxis = Vector2.zero;
    private Vector2 recoil = Vector2.zero;

    private FirstPersonController fpsController;

    private void Start()
    {
        fpsController = GetComponentInParent<FirstPersonController>();

        currentAmmoInClip = clipSize;
        ammoInReserve = reservedAmmoCapacity;
    }

    private void Update()
    {
        HandleAim();

        WeaponSway();

        if(Input.GetKey(fireKey) && CanShoot && currentAmmoInClip > 0)
        {
            CanShoot = false;
            currentAmmoInClip--;
            StartCoroutine(ShootGun());
        }
        else if(Input.GetKeyDown(reloadKey) && currentAmmoInClip < clipSize && ammoInReserve > 0)
        {
            int amountNeeded = clipSize - currentAmmoInClip;
            if(amountNeeded >= ammoInReserve)
            {
                currentAmmoInClip += ammoInReserve;
                ammoInReserve -= amountNeeded;
            }
            else
            {
                currentAmmoInClip = clipSize;
                ammoInReserve -= amountNeeded;
            }
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

        if(randomizeRecoil)
        {
            float xRecoil = Random.Range(-randomReacoilConstraints.x, randomReacoilConstraints.x);
            float yRecoil = Random.Range(-randomReacoilConstraints.y, randomReacoilConstraints.y);

            recoil = new Vector2(xRecoil, yRecoil);

            fpsController.currentRotation += recoil;
        }
        else
        {
            int currentStep = clipSize + 1 - currentAmmoInClip;
            currentStep = Mathf.Clamp(currentStep, 0, recoilPattern.Length - 1);

            fpsController.currentRotation += recoilPattern[currentStep];
        }
    }

    private void RaycastForEnemy()
    {
        if(Physics.Raycast(fpsController.playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
        {
            Debug.Log(hit.collider.name);
        }
    }

    private IEnumerator ShootGun()
    {
        HandleRecoil();
        muzzleFlash.Play();
        gunAudioSource.PlayOneShot(shootAudioClip);

        RaycastForEnemy();

        yield return new WaitForSeconds(fireRate);
        CanShoot = true;
    }
}
