using UnityEngine;

/// <summary>
/// LOCOMOTION SYSTEM - Handles all movement states and transitions
/// This is the foundation of how your character moves
/// 
/// States: Idle -> Walking -> Running -> Sprinting -> Crouching
/// Also handles: stamina, acceleration curves, foot IK on terrain
/// </summary>
public class LocomotionSystem : MonoBehaviour
{
    // ===== LOCOMOTION STATES =====
    public enum LocomotionState
    {
        Idle,
        Walking,
        Running,
        Sprinting,
        Crouching,
        Falling,
        Landing
    }

    public LocomotionState currentState = LocomotionState.Idle;
    private LocomotionState previousState;

    // ===== MOVEMENT PARAMETERS =====
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float sprintSpeed = 12f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float acceleration = 15f;      // How fast we reach target speed
    [SerializeField] private float deceleration = 10f;      // How fast we stop
    [SerializeField] private float rotationSpeed = 10f;     // How smoothly we rotate

    // ===== STAMINA SYSTEM =====
    [SerializeField] private float maxStamina = 100f;
    private float currentStamina;
    [SerializeField] private float sprintStaminaDrain = 20f;    // Per second
    [SerializeField] private float staminaRegenRate = 15f;      // Per second
    private float staminaRegenDelay = 0.5f;                     // Delay before regen starts
    private float timeSinceLastSprint = 0f;

    // ===== PHYSICS & GRAVITY =====
    [SerializeField] private float groundDrag = 0.1f;
    [SerializeField] private float airDrag = 0.05f;
    [SerializeField] private float fallAcceleration = 9.81f;
    [SerializeField] private float maxFallSpeed = 50f;
    private float verticalVelocity = 0f;

    // ===== GROUND DETECTION =====
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded = false;
    private RaycastHit groundHit;

    // ===== INPUT STORAGE =====
    private float inputX = 0f;
    private float inputZ = 0f;
    private bool wantsSprint = false;
    private bool wantsCrouch = false;
    private bool wantsJump = false;

    // ===== CURRENT MOVEMENT =====
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetDirection = Vector3.zero;
    private float currentSpeed = 0f;

    // ===== ANIMATION =====
    private int speedHash;
    private int directionHash;
    private int stateHash;

    // ===== SLOPE HANDLING =====
    [SerializeField] private float maxSlopeAngle = 45f;
    private float currentSlopeAngle = 0f;

    private void Start()
    {
        currentStamina = maxStamina;
        
        // Cache animator parameter hashes for performance
        speedHash = Animator.StringToHash("Speed");
        directionHash = Animator.StringToHash("Direction");
        stateHash = Animator.StringToHash("State");
    }

    /// <summary>
    /// Receives input from PlayerController
    /// </summary>
    public void HandleInput(float moveX, float moveZ, bool sprint, bool jump, bool crouch)
    {
        inputX = moveX;
        inputZ = moveZ;
        wantsSprint = sprint;
        wantsCrouch = crouch;
        wantsJump = jump;
    }

    /// <summary>
    /// Main locomotion update - called every frame
    /// </summary>
    public void UpdateLocomotion(CharacterController controller, Animator animator)
    {
        // STEP 1: Check if grounded
        CheckGroundState(controller);

        // STEP 2: Determine movement direction
        DetermineMovementDirection();

        // STEP 3: Calculate target speed based on input
        float targetSpeed = CalculateTargetSpeed();

        // STEP 4: Update stamina
        UpdateStamina(targetSpeed);

        // STEP 5: Smoothly interpolate to target speed (acceleration/deceleration)
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, 
            (targetSpeed > currentSpeed ? acceleration : deceleration) * Time.deltaTime);

        // STEP 6: Calculate velocity
        Vector3 movementVelocity = targetDirection * currentSpeed;

        // STEP 7: Apply gravity
        if (isGrounded)
        {
            verticalVelocity = -0.5f; // Small negative to keep grounded
        }
        else
        {
            verticalVelocity = Mathf.Max(verticalVelocity - (fallAcceleration * Time.deltaTime), -maxFallSpeed);
        }

        movementVelocity.y = verticalVelocity;

        // STEP 8: Rotate character smoothly to face movement direction
        if (currentSpeed > 0.1f)
        {
            RotateTowardsDirection(targetDirection, rotationSpeed);
        }

        // STEP 9: Apply movement to controller
        controller.Move(movementVelocity * Time.deltaTime);

        // STEP 10: Update state and animations
        UpdateLocomotionState();
        UpdateAnimations(animator);

        // STEP 11: Handle jumping
        if (wantsJump && isGrounded)
        {
            Jump();
        }
    }

    /// <summary>
    /// Check if player is grounded using raycast
    /// </summary>
    private void CheckGroundState(CharacterController controller)
    {
        // Cast a ray downward from player position
        Vector3 rayStart = transform.position + Vector3.up * controller.radius;
        
        isGrounded = Physics.Raycast(rayStart, Vector3.down, out groundHit, 
            groundCheckDistance + controller.height * 0.5f, groundLayer);

        // Calculate slope angle for downhill/uphill detection
        if (isGrounded)
        {
            currentSlopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        }
    }

    /// <summary>
    /// Determine which direction the player wants to move
    /// </summary>
    private void DetermineMovementDirection()
    {
        // Get camera-relative direction (this assumes a camera exists)
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            targetDirection = new Vector3(inputX, 0, inputZ).normalized;
            return;
        }

        // Forward relative to camera
        Vector3 cameraForward = mainCam.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        // Right relative to camera
        Vector3 cameraRight = mainCam.transform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();

        // Combine inputs relative to camera
        targetDirection = (cameraForward * inputZ + cameraRight * inputX).normalized;

        // If no input, maintain previous direction for smooth stopping
        if (inputX == 0 && inputZ == 0)
        {
            targetDirection = Vector3.Lerp(targetDirection, Vector3.zero, Time.deltaTime * 5f);
        }
    }

    /// <summary>
    /// Calculate what speed we should be moving at based on input and state
    /// </summary>
    private float CalculateTargetSpeed()
    {
        // No input = no movement
        if (inputX == 0 && inputZ == 0)
            return 0f;

        // Crouching takes priority
        if (wantsCrouch)
            return crouchSpeed;

        // Sprinting (if we have stamina)
        if (wantsSprint && currentStamina > 5f)
            return sprintSpeed;

        // Normal running
        if (Mathf.Abs(inputZ) > 0.5f) // Forward movement detected
            return runSpeed;

        // Walking (slower)
        return walkSpeed * 0.7f;
    }

    /// <summary>
    /// Update stamina based on current activity
    /// </summary>
    private void UpdateStamina(float speed)
    {
        // Drain stamina while sprinting
        if (wantsSprint && speed > runSpeed && isGrounded)
        {
            currentStamina -= sprintStaminaDrain * Time.deltaTime;
            timeSinceLastSprint = 0f;
        }
        else
        {
            timeSinceLastSprint += Time.deltaTime;
        }

        // Regenerate stamina after delay
        if (timeSinceLastSprint > staminaRegenDelay && currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
        }

        // Clamp stamina
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
    }

    /// <summary>
    /// Jump mechanics
    /// </summary>
    private void Jump()
    {
        if (!isGrounded)
            return;

        // Jump velocity formula: v = sqrt(2 * g * h)
        // For now, just give it a fixed upward velocity
        verticalVelocity = 10f;
        isGrounded = false;
        currentState = LocomotionState.Falling;
    }

    /// <summary>
    /// Rotate character to face movement direction smoothly
    /// </summary>
    private void RotateTowardsDirection(Vector3 direction, float speed)
    {
        if (direction.magnitude < 0.1f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, speed * Time.deltaTime);
    }

    /// <summary>
    /// Determine current locomotion state
    /// </summary>
    private void UpdateLocomotionState()
    {
        previousState = currentState;

        if (!isGrounded)
        {
            currentState = verticalVelocity < -2f ? LocomotionState.Falling : LocomotionState.Falling;
            return;
        }

        if (wantsCrouch)
        {
            currentState = LocomotionState.Crouching;
            return;
        }

        if (currentSpeed < 0.1f)
        {
            currentState = LocomotionState.Idle;
            return;
        }

        if (wantsSprint && currentStamina > 5f)
        {
            currentState = LocomotionState.Sprinting;
            return;
        }

        if (currentSpeed > walkSpeed * 1.5f)
        {
            currentState = LocomotionState.Running;
            return;
        }

        currentState = LocomotionState.Walking;
    }

    /// <summary>
    /// Update animator parameters
    /// </summary>
    private void UpdateAnimations(Animator animator)
    {
        if (animator == null)
            return;

        // Speed (0-1 normalized)
        animator.SetFloat(speedHash, currentSpeed / sprintSpeed);

        // Direction blending (forward/strafe/backward)
        float directionBlend = Mathf.Atan2(inputX, inputZ) * Mathf.Rad2Deg;
        animator.SetFloat(directionHash, directionBlend);

        // State
        animator.SetInteger(stateHash, (int)currentState);
    }

    // ===== PUBLIC GETTERS =====
    public float GetCurrentSpeed() => currentSpeed;
    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina() => maxStamina;
    public LocomotionState GetCurrentState() => currentState;
    public bool GetIsGrounded() => isGrounded;
}
