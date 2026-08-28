using UnityEngine;

/// <summary>
/// PLAYER CONTROLLER - The central hub that manages all player systems
/// Think of this as the "conductor" that orchestrates locomotion, combat, traversal, and health
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ===== SYSTEMS REFERENCES =====
    // These are like different departments in a company - each manages its own area
    public LocomotionSystem locomotion { get; private set; }
    public CombatSystem combat { get; private set; }
    public HealthSystem health { get; private set; }
    public TraversalSystem traversal { get; private set; }

    // ===== INPUT & MOVEMENT =====
    private CharacterController characterController;
    private Animator animator;

    // ===== INITIALIZATION =====
    private void Awake()
    {
        // Get required components
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Initialize all systems (they'll set themselves up)
        locomotion = GetComponent<LocomotionSystem>();
        combat = GetComponent<CombatSystem>();
        health = GetComponent<HealthSystem>();
        traversal = GetComponent<TraversalSystem>();

        // Verify all systems exist
        if (locomotion == null) Debug.LogError("LocomotionSystem not found!");
        if (combat == null) Debug.LogError("CombatSystem not found!");
        if (health == null) Debug.LogError("HealthSystem not found!");
        if (traversal == null) Debug.LogError("TraversalSystem not found!");
    }

    private void Update()
    {
        // Get player input
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        bool jump = Input.GetKeyDown(KeyCode.Space);
        bool crouch = Input.GetKey(KeyCode.C);
        bool attack = Input.GetMouseButtonDown(0);
        bool dodge = Input.GetMouseButtonDown(1);
        bool vault = Input.GetKeyDown(KeyCode.V);
        bool wallRun = Input.GetKeyDown(KeyCode.W);

        // Feed input to systems
        locomotion.HandleInput(moveX, moveZ, sprint, jump, crouch);
        combat.HandleInput(attack, dodge);
        traversal.HandleInput(vault, wallRun);

        // Update all systems
        locomotion.UpdateLocomotion(characterController, animator);
        combat.UpdateCombat();
        traversal.UpdateTraversal();
    }

    /// <summary>
    /// Called when player takes damage - health system broadcasts this
    /// </summary>
    public void OnPlayerDamaged(float damageAmount)
    {
        health.TakeDamage(damageAmount);
        // Stop combat actions if heavily damaged
        if (health.GetCurrentHealth() < health.GetMaxHealth() * 0.3f)
        {
            combat.InterruptCombo();
        }
    }

    /// <summary>
    /// Called when player recovers health
    /// </summary>
    public void OnPlayerHealed(float healAmount)
    {
        health.Heal(healAmount);
    }

    /// <summary>
    /// Disables player control (cutscenes, etc)
    /// </summary>
    public void DisableControl()
    {
        locomotion.enabled = false;
        combat.enabled = false;
        traversal.enabled = false;
    }

    /// <summary>
    /// Re-enables player control
    /// </summary>
    public void EnableControl()
    {
        locomotion.enabled = true;
        combat.enabled = true;
        traversal.enabled = true;
    }
}
