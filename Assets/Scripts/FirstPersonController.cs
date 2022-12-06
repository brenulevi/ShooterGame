using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
  public bool CanMove { get; private set; } = true;
  private bool IsSprinting => CanSprint && Input.GetKey(sprintKey);
  private bool ShouldJump => Input.GetKeyDown(jumpKey) && characterController.isGrounded;
  private bool ShouldCrouch => Input.GetKeyDown(crouchKey) && !duringCrouchAnimation && characterController.isGrounded;

  [Header("Functional Options")]
  [SerializeField] private bool CanSprint = true;
  [SerializeField] private bool CanJump = true;
  [SerializeField] private bool CanCrouch = true;
  [SerializeField] private bool CanUseHeadBob = true;
  [SerializeField] private bool WillSlideOnSlopes = true;
  [SerializeField] public bool CanZoom = true;
  [SerializeField] private bool CanInteract = true;
  [SerializeField] private bool UseFootsteps = true;
  [SerializeField] private bool UseStamina = true;

  [Header("Controls")]
  [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
  [SerializeField] private KeyCode jumpKey = KeyCode.Space;
  [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
  [SerializeField] private KeyCode zoomKey = KeyCode.Mouse1;
  [SerializeField] private KeyCode interactKey = KeyCode.E;

  [Header("Movement Parameters")]
  [SerializeField] private float walkSpeed = 3.0f;
  [SerializeField] private float sprintSpeed = 6.0f;
  [SerializeField] private float crouchSpeed = 1.5f;
  [SerializeField] private float slopeSpeed = 8f;

  [Header("Look Parameters")]
  [SerializeField, Range(1, 10)] private float lookSpeedX = 2.0f;
  [SerializeField, Range(1, 10)] private float lookSpeedY = 2.0f;
  [SerializeField, Range(1, 180)] private float upperLookLimit = 80.0f;
  [SerializeField, Range(1, 180)] private float lowerLookLimit = 80.0f;

  [HideInInspector] public Vector2 currentRotation = Vector2.zero;

  [Header("Health Parameters")]
  [SerializeField] private float maxHealth = 100f;
  [SerializeField] private float timeBeforeRegenStarts = 3f;
  [SerializeField] private float healthValueIncrement = 1f;
  [SerializeField] private float healthTimeIncrement = .1f;
  private float currentHealth;
  private Coroutine regeneratingHealth;
  public static Action<float> OnTakeDamage;
  public static Action<float> OnDamage;
  public static Action<float> OnHeal;

  [Header("Stamina Parameters")]
  [SerializeField] private float maxStamina = 100f;
  [SerializeField] private float staminaUseMultiplier = 5f;
  [SerializeField] private float timeBeforeStaminaRegenStarts = 5f;
  [SerializeField] private float staminaValueIncrement = 2f;
  [SerializeField] private float staminaTimeIncrement = .1f;
  private float currentStamina;
  private Coroutine regeneratingStamina;
  public static Action<float> OnStaminaChange;

  [Header("Jumping Parameters")]
  [SerializeField] private float jumpForce = 8.0f;
  [SerializeField] private float gravity = 30.0f;

  [Header("Crouch Parameters")]
  [SerializeField] private float crouchHeight = .5f;
  [SerializeField] private float standHeight = 2f;
  [SerializeField] private float timeToCrouch = .25f;
  [SerializeField] private Vector3 crouchingCenter = new Vector3(0, .5f, 0);
  [SerializeField] private Vector3 standingCenter = new Vector3(0, 0, 0);
  private bool IsCrouching;
  private bool duringCrouchAnimation;

  [Header("Headbob Parameters")]
  [SerializeField] private float walkBobSpeed = 14f;
  [SerializeField] private float walkBobAmount = .05f;
  [SerializeField] private float sprintBobSpeed = 18f;
  [SerializeField] private float sprintBobAmount = .1f;
  [SerializeField] private float crouchBobSpeed = 8f;
  [SerializeField] private float crouchBobAmount = .025f;
  private float defaultYPos = 0;
  private float timer;

  [Header("Zoom Parameters")]
  [SerializeField] private float timeToZoom = .3f;
  [SerializeField] private float zoomFOV = 30f;
  private float defaultFOV;
  private Coroutine zoomRoutine;
  public bool isZooming = false;

  [Header("Footstep Parameters")]
  [SerializeField] private float baseStepSpeed = .5f;
  [SerializeField] private float crouchStepMultiplier = 1.5f;
  [SerializeField] private float sprintStepMultiplier = .6f;
  [SerializeField] private AudioSource footstepAudioSource = default;
  [SerializeField] private AudioClip[] concreteClips = default;
  [SerializeField] private AudioClip[] woodClips = default;
  [SerializeField] private AudioClip[] metalClips = default;
  [SerializeField] private AudioClip[] grassClips = default;
  private float footstepTimer = 0;
  private float GetCurrentOffset => IsCrouching ? baseStepSpeed * crouchStepMultiplier : IsSprinting ? baseStepSpeed * sprintStepMultiplier : baseStepSpeed;

  // SLIDING PARAMETERS
  private Vector3 hitPointNormal;
  private bool IsSliding
  {
    get
    {
      if (characterController.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 2f))
      {
        hitPointNormal = slopeHit.normal;
        return Vector3.Angle(hitPointNormal, Vector3.up) > characterController.slopeLimit;
      }
      else
      {
        return false;
      }
    }
  }

  [Header("Interaction")]
  [SerializeField] private Vector3 interactionRayPoint = default;
  [SerializeField] private float interactionDistance = default;
  [SerializeField] private LayerMask interactionLayer = default;
  private Interactable currentInteractable;

  [HideInInspector] public Camera playerCamera;
  private CharacterController characterController;

  private Vector3 moveDirection;
  private Vector2 currentInput;

  private float rotationX = 0;

  private void OnEnable()
  {
    OnTakeDamage += ApplyDamage;
  }

  private void OnDisable()
  {
    OnTakeDamage -= ApplyDamage;
  }

  private void Awake()
  {
    // Player Properties
    playerCamera = GetComponentInChildren<Camera>();
    characterController = GetComponent<CharacterController>();

    defaultYPos = playerCamera.transform.localPosition.y;
    defaultFOV = playerCamera.fieldOfView;
    currentHealth = maxHealth;
    currentStamina = maxStamina;

    // Cursor
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
  }

  private void Update()
  {
    if (CanMove)
    {
      HandleMovementInput();
      HandleMouseLook();

      if (CanJump)
        HandleJump();

      if (CanCrouch)
        HandleCrouch();

      if (CanUseHeadBob)
        HandleHeadbob();

      if (CanZoom)
        HandleZoom();

      if (CanInteract)
      {
        HandleInteractCheck();
        HandleInteractInput();
      }

      if (UseFootsteps)
        HandleFootsteps();

      if (UseStamina)
        HandleStamina();

      ApplyFinalMovements();
    }
  }

  private void HandleMovementInput()
  {
    currentInput = new Vector2(Input.GetAxis("Vertical"), Input.GetAxis("Horizontal")).normalized;
    currentInput *= (IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed);

    float moveDirectionY = moveDirection.y;
    moveDirection = (transform.TransformDirection(Vector3.forward) * currentInput.x) + (transform.TransformDirection(Vector3.right) * currentInput.y);
    moveDirection.y = moveDirectionY;
  }

  private void HandleMouseLook()
  {
    Vector2 mouseAxis = new Vector2(Input.GetAxis("Mouse X") * lookSpeedX, Input.GetAxis("Mouse Y") * lookSpeedY);

    currentRotation += mouseAxis;

    currentRotation.y = Mathf.Clamp(currentRotation.y, -upperLookLimit, lowerLookLimit);

    transform.localRotation = Quaternion.AngleAxis(currentRotation.x, Vector3.up);
    playerCamera.transform.localRotation = Quaternion.AngleAxis(-currentRotation.y, Vector3.right);

    // Old camera movement logic (changed because recoil)
    //rotationX -= Input.GetAxis("Mouse Y") * lookSpeedY;
    //rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit);
    //playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
    //transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeedX, 0);
  }

  private void HandleJump()
  {
    if (ShouldJump)
      moveDirection.y = jumpForce;
  }

  private void HandleCrouch()
  {
    if (ShouldCrouch)
      StartCoroutine(CrouchStand());
  }

  private void HandleHeadbob()
  {
    if (!characterController.isGrounded)
      return;

    if (Mathf.Abs(moveDirection.x) > .1f || Mathf.Abs(moveDirection.z) > .1f)
    {
      timer += Time.deltaTime * (IsCrouching ? crouchBobSpeed : IsSprinting ? sprintBobSpeed : walkBobSpeed);
      playerCamera.transform.localPosition = new Vector3(
          playerCamera.transform.localPosition.x,
          defaultYPos + Mathf.Sin(timer) * (IsCrouching ? crouchBobAmount : IsSprinting ? sprintBobAmount : walkBobAmount),
          playerCamera.transform.localPosition.z
      );
    }

  }

  private void HandleZoom()
  {
    if (Input.GetKeyDown(zoomKey) && !isZooming)
    {
      if (zoomRoutine != null)
      {
        StopCoroutine(zoomRoutine);
        zoomRoutine = null;
      }

      isZooming = true;
      zoomRoutine = StartCoroutine(ToggleZoom(true));
    }

    if (Input.GetKeyUp(zoomKey) && isZooming)
    {
      if (zoomRoutine != null)
      {
        StopCoroutine(zoomRoutine);
        zoomRoutine = null;
      }

      isZooming = false;
      zoomRoutine = StartCoroutine(ToggleZoom(false));
    }
  }

  private void HandleInteractCheck()
  {
    if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out RaycastHit hit, interactionDistance))
    {
      if (hit.collider.gameObject.layer == 7 && (currentInteractable == null || hit.collider.gameObject.GetInstanceID() != currentInteractable.GetInstanceID()))
      {
        hit.collider.TryGetComponent(out currentInteractable);

        if (currentInteractable)
          currentInteractable.OnFocus();
      }
    }
    else if (currentInteractable)
    {
      currentInteractable.OnLoseFocus();
      currentInteractable = null;
    }
  }

  private void HandleInteractInput()
  {
    if (Input.GetKeyDown(interactKey) && currentInteractable != null && Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out RaycastHit hit, interactionDistance, interactionLayer))
    {
      currentInteractable.OnInteract();
    }
  }

  private void HandleFootsteps()
  {
    if (!characterController.isGrounded) return;
    if (currentInput == Vector2.zero) return;

    footstepTimer -= Time.deltaTime;

    if (footstepTimer <= 0)
    {
      if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 3f))
      {
        switch (hit.collider.tag)
        {
          case "Footsteps/Wood":
            footstepAudioSource.PlayOneShot(woodClips[UnityEngine.Random.Range(0, woodClips.Length - 1)]);
            break;
          case "Footsteps/Metal":
            footstepAudioSource.PlayOneShot(metalClips[UnityEngine.Random.Range(0, metalClips.Length - 1)]);
            break;
          case "Footsteps/Grass":
            footstepAudioSource.PlayOneShot(grassClips[UnityEngine.Random.Range(0, grassClips.Length - 1)]);
            break;
          default:
            footstepAudioSource.PlayOneShot(concreteClips[UnityEngine.Random.Range(0, concreteClips.Length - 1)]);
            break;
        }
      }
      footstepTimer = GetCurrentOffset;
    }
  }

  private void HandleStamina()
  {
    if (IsSprinting && currentInput != Vector2.zero)
    {
      if (regeneratingStamina != null)
      {
        StopCoroutine(regeneratingStamina);
        regeneratingStamina = null;
      }

      currentStamina -= staminaUseMultiplier * Time.deltaTime;

      if (currentStamina < 0)
        currentStamina = 0;

      OnStaminaChange?.Invoke(currentStamina);

      if (currentStamina <= 0)
        CanSprint = false;
    }

    if (!IsSprinting && currentStamina < maxStamina && regeneratingStamina == null)
    {
      regeneratingStamina = StartCoroutine(RegenerateStamina());
    }
  }

  private void ApplyFinalMovements()
  {
    if (!characterController.isGrounded)
      moveDirection.y -= gravity * Time.deltaTime;

    if (WillSlideOnSlopes && IsSliding)
      moveDirection += new Vector3(hitPointNormal.x, -hitPointNormal.y, hitPointNormal.z) * slopeSpeed;

    characterController.Move(moveDirection * Time.deltaTime);
  }

  private void ApplyDamage(float dmg)
  {
    currentHealth -= dmg;
    OnDamage?.Invoke(currentHealth);

    if (currentHealth <= 0)
      KillPlayer();
    else if (regeneratingHealth != null)
      StopCoroutine(regeneratingHealth);

    regeneratingHealth = StartCoroutine(RegenerateHealth());
  }

  private void KillPlayer()
  {
    currentHealth = 0;
    if (regeneratingHealth != null)
      StopCoroutine(regeneratingHealth);

    // Do whatever I want to death
    print("DEAD");
  }

  private IEnumerator CrouchStand()
  {
    // Verify if have anything above the player to stand up
    if (IsCrouching && Physics.Raycast(playerCamera.transform.position, Vector3.up, 1f))
      yield break;

    duringCrouchAnimation = true;

    float timeElapsed = 0;
    float targetHeight = IsCrouching ? standHeight : crouchHeight;
    float currentHeight = characterController.height;
    Vector3 targetCenter = IsCrouching ? standingCenter : crouchingCenter;
    Vector3 currentCenter = characterController.center;

    while (timeElapsed < timeToCrouch)
    {
      characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch);
      characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
      timeElapsed += Time.deltaTime;
      yield return null;
    }

    // For exact values and not frac, we set after the while because .Lerp will vary constantly
    characterController.height = targetHeight;
    characterController.center = targetCenter;

    IsCrouching = !IsCrouching;

    duringCrouchAnimation = false;
  }

  private IEnumerator ToggleZoom(bool isEnter)
  {
    float targetFOV = isEnter ? zoomFOV : defaultFOV;
    float startingFOV = playerCamera.fieldOfView;
    float timeElapsed = 0;

    while (timeElapsed < timeToZoom)
    {
      playerCamera.fieldOfView = Mathf.Lerp(startingFOV, targetFOV, timeElapsed / timeToZoom);
      timeElapsed += Time.deltaTime;
      yield return null;
    }

    playerCamera.fieldOfView = targetFOV;
    zoomRoutine = null;
  }

  private IEnumerator RegenerateHealth()
  {
    yield return new WaitForSeconds(timeBeforeRegenStarts);
    WaitForSeconds timeToWait = new WaitForSeconds(healthTimeIncrement);

    while (currentHealth < maxHealth)
    {
      currentHealth += healthValueIncrement;

      if (currentHealth > maxHealth)
        currentHealth = maxHealth;

      OnHeal?.Invoke(currentHealth);
      yield return timeToWait;
    }

    regeneratingHealth = null;
  }

  private IEnumerator RegenerateStamina()
  {
    yield return new WaitForSeconds(timeBeforeStaminaRegenStarts);
    WaitForSeconds timeToWait = new WaitForSeconds(staminaTimeIncrement);

    while (currentStamina < maxStamina)
    {
      if (currentStamina > 0)
        CanSprint = true;

      currentStamina += staminaValueIncrement;

      if (currentStamina > maxStamina)
        currentStamina = maxStamina;

      OnStaminaChange?.Invoke(currentStamina);

      yield return timeToWait;
    }

    regeneratingStamina = null;
  }
}
