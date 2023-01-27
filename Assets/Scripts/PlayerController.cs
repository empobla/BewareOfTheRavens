// ISTA 425 / INFO 525 Algorithms for Games
//
// Player Controller

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Tooltip("Frame-rate independent movement")]
    public float moveRate = 5.0f;

    [Tooltip("Player-relative ortho camera")]
    public GameObject playerCamera;

    [Tooltip("Time to fade sounds end of action, in seconds")]
    public float fadeTime = 0.1f;

    // Jump
    [Tooltip("Jump height of the player")]
    public float jumpForce = 2.0f;

    // Hack
    [Tooltip("Hack attack range, in units")]
    public float hackRange = 2.3f;

    // Cast
    [Tooltip("Spell radius, in units")]
    public float spellRadius = 8f;

    [Tooltip("Spell cooldown, in seconds")][Min(0.66f)]
    public float spellCooldown = 4f;


    // For jump
    float groundY;
    float gravity;
    bool isJumping = false;
    Vector3 velocity = Vector3.zero;


    // For hack
    [HideInInspector]
    public float lookDirection = 1f;
    bool isHacking = false;
    float hackTimer = 0f;


    // For cast
    bool isCasting = false;
    float castTimer = 0f;
    float timeUntilNextCast = 0f;


    GameController eventSystem;
    Animator       wizardAnim;
    SpriteRenderer wizardSprite;

    enum ActionType
    {
        Cast,
        Hack,
        Jump,
        Die
    }

    // Reality check: Is this character alive?
    bool dead = false;

    // Animation state machine metadata
    int runTrigger    = Animator.StringToHash("isRunning");
    int idleTrigger   = Animator.StringToHash("isIdling");
    int jumpTrigger   = Animator.StringToHash("isJumping");
    int fallTrigger   = Animator.StringToHash("isFalling");
    int deathTrigger  = Animator.StringToHash("isDying");

    int attackTrigger = Animator.StringToHash("isAttacking");
    int attackType    = Animator.StringToHash("attackType");

    // Private method sends messages to the animation state machine based
    // on the current user input for running, attacking, etc.
    private void SetAnimState (float x, float y)
    {
        bool cast = false;
        bool hack = false;
        bool jump = false;
        bool die  = false;

        // Get new inputs only if wizard is not dead
        if (!dead)
        {
            cast = eventSystem.getInput(GameController.ControlType.Cast);
            hack = eventSystem.getInput(GameController.ControlType.Hack);
            jump = eventSystem.getInput(GameController.ControlType.Jump);
            die  = eventSystem.getInput(GameController.ControlType.Die);
        }

        else {
            die = true;
        }

        // Set the state of the controller
        AnimatorStateInfo state = wizardAnim.GetCurrentAnimatorStateInfo(0);

        if (die)
        {
            wizardAnim.SetTrigger(deathTrigger);
            dead = true;
        }

        if (jump)
            wizardAnim.SetTrigger(jumpTrigger);

        // Set movement state if wizard is moving left or right
        if (x != 0.0f)
        {
            // face the direction of move
            if (x > 0.0f)
                wizardSprite.flipX = false;
            else if (x < 0.0f)
                wizardSprite.flipX = true;

            wizardAnim.SetTrigger(runTrigger);
            wizardAnim.ResetTrigger(Animator.StringToHash("isAttacking"));
        }
        // The wizard is standing still, idling
        else if (x == 0.0f && y == 0.0f)
        {
            wizardAnim.SetTrigger(idleTrigger);

            // Spell casting takes precendence over attacking
            if (isCasting)
            {
                wizardAnim.SetInteger(attackType, (int)ActionType.Cast);
                wizardAnim.SetTrigger(attackTrigger);
                isCasting = false;
            }
            else if (isHacking)
            {
                wizardAnim.SetInteger(attackType, (int)ActionType.Hack);
                wizardAnim.SetTrigger(attackTrigger);
                isHacking = false;
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        eventSystem = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameController>();
        gravity = eventSystem.gravity;

        wizardAnim = GetComponent<Animator>();
        wizardSprite = GetComponent<SpriteRenderer>();

        groundY = transform.position.y;
    }

    // Update is called once per frame
    void Update()
    {
        float x = 0.0f, y = 0.0f;
        bool jump = false, hack = false, cast = false;

        // Get new inputs only if wizard is not dead
        if (!dead)
        {
            // Do not get input from movement keys if wizard is performing 
            // an action
            if (hackTimer <= 0 && castTimer <= 0)
            {
                // Input from up/down, left/right keys
                x = eventSystem.getAxis(GameController.AxisType.X);
                y = eventSystem.getAxis(GameController.AxisType.Y);
            }

            // Input from jumping and casting
            jump = eventSystem.getInput(GameController.ControlType.Jump);
            hack = eventSystem.getInput(GameController.ControlType.Hack);
            cast = eventSystem.getInput(GameController.ControlType.Cast);
        }

        // Setup the wizard's current animation state.
        SetAnimState(x, y);

        // Handle wizard's movement
        Vector3 move = new Vector3(x, 0.0f, 0.0f) * moveRate * Time.deltaTime;
        bool moving = move != Vector3.zero;
        if (moving)
            Move(move, x);

        // Handle wizard's collisions
        bool collisionHappened = eventSystem.collisions.Count > 0;
        if (collisionHappened)
            HandleCollisions();

        // Handle wizard's jump
        if (jump && IsGrounded())
            isJumping = true;

        // Handle wizard's hack
        if (hack) 
        {
            hackTimer = 0.66f;
            isHacking = true;
        }
        if (hackTimer > 0) hackTimer -= Time.deltaTime;

        // Handle wizard's cast
        if (cast && timeUntilNextCast <= 0) CastSpell();
        if (castTimer > 0) castTimer -= Time.deltaTime;
        if (timeUntilNextCast > 0) timeUntilNextCast -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        // Jump
        if (isJumping)
        {
            // Following the following formula to find how much velocity is needed to jump a certain height:
            // v = sqrt(-2*h*g), where h is height and g is gravity.
            velocity.y = Mathf.Sqrt(-2 * jumpForce * gravity);
            transform.position += velocity * Time.deltaTime;
            isJumping = false;
        }

        // Apply gravity when wizard is in the air
        if (!isJumping && !IsGrounded())
            // Constant gravity following deltaY = 1/2g * t^2
            velocity.y += gravity * Time.deltaTime;

        // When wizard is in the ground, reset vertical velocity and clamp to ground
        if (IsGrounded())
        {
            velocity.y = 0f;
            if (transform.position.y != groundY || transform.position.y < groundY - 0.1f)
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
        }

        transform.position += velocity * Time.deltaTime;
    }

    void Move(Vector3 move, float x)
    {
        // increment the scroller position for the background sprites
        float totalMove = eventSystem.playerMove.x + move.x;
        float clampMove = eventSystem.clamp(totalMove);

        eventSystem.scrollerMove.x = clampMove;
        eventSystem.playerMove.x = totalMove;

        // TODO: Part of the parallax scrolling algorithm may go here.
        // !! Didn't put parallax here.

        // Save move direction (also look direction)
        lookDirection = x;
    }

    void HandleCollisions()
    {
        foreach (GameController.Collision col in eventSystem.collisions)
        {
            if (col.go1.GetInstanceID() == gameObject.GetInstanceID() || col.go2.GetInstanceID() == gameObject.GetInstanceID())
                {
                    GameObject collider = col.go1.GetInstanceID() != gameObject.GetInstanceID() ? col.go1 : col.go2;
                    // If player collided with a fireball, he dies
                    if (collider.tag == "Fireball")
                    {
                        dead = true;
                        return;
                    }

                    // The player collided with a raven
                    
                    bool hack = eventSystem.getInput(GameController.ControlType.Hack);
                    float x = eventSystem.getAxis(GameController.AxisType.X);
                    float y = eventSystem.getAxis(GameController.AxisType.Y);

                    bool lookingRight = lookDirection == 1;
                    bool ravenRight = Mathf.Sign(collider.transform.position.x - transform.position.x) > 0;
                    bool notRunning = x == 0.0f && y == 0.0f;

                    // Player is hacking and the raven is in the same direction of the attack
                    if (hack && notRunning && lookingRight == ravenRight)
                    {
                        GameObject.Destroy(collider);
                        return;
                    }

                    // Player got hit and couldn't attack on time
                    dead = true;
                }
        }
    }

    bool IsGrounded()
    {
        return Mathf.Abs(groundY - transform.position.y) <= 0.1f;
    }

    void CastSpell()
    {
        // Enforce spell cooldown
        timeUntilNextCast = spellCooldown;

        // Start animation timer
        castTimer = 0.66f;
        isCasting = true;

        // Get all enemy objects
        List<GameObject> possibleTargets = new List<GameObject>();
        GameObject[] ravens = GameObject.FindGameObjectsWithTag("Raven");
        GameObject[] fireballs = GameObject.FindGameObjectsWithTag("Fireball");
        foreach (GameObject raven in ravens) possibleTargets.Add(raven);
        foreach (GameObject fireball in fireballs) possibleTargets.Add(fireball);

        // Destroy enemy objects in spell range
        foreach(GameObject target in possibleTargets)
            if (Vector3.Distance(target.transform.position, transform.position) <= spellRadius)
                GameObject.Destroy(target);
    }

    // void OnDrawGizmosSelected()
    // {
    //     Gizmos.color = new Color(0.5f, 0, 0, 0.4f);
    //     Gizmos.DrawSphere(transform.position, SpellRadius);
    // }
}