using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Controls all player movement mechanics including running, jumping, wall sliding, wall jumping, and dashing
/// </summary>
public class PlayerMovement : MonoBehaviour
{

#region Variables
    [Header("References")]
    // ScriptableObject containing all movement-related parameters
    public PlayerMovementStats MoveStats;
    // Colliders for feet and body hit detection
    [SerializeField] private Collider2D _feetColl;
    [SerializeField] private Collider2D _bodyColl;

    // Core components
    private Rigidbody2D _rb;
    private Animator _animator;

    // Basic movement state
    public float HorizontalVelocity { get; private set; }
    private bool _isFacingRight; // Tracks which direction player is facing

    // Collision detection rays/states
    private RaycastHit2D _groundHit;  // Ray for ground detection
    private RaycastHit2D _headHit;    // Ray for ceiling detection
    private RaycastHit2D _wallHit;    // Ray for wall detection
    private RaycastHit2D _lastWallHit;// Stores last wall touched for wall jump direction
    private bool _isGrounded;         // True when touching ground
    private bool _bumpedHead;         // True when hitting ceiling
    private bool _isTouchingWall;     // True when touching wall

    // Jump-related variables
    public float VerticalVelocity { get; private set; }
    private bool _isJumping;          // True during jump
    private bool _isFastFalling;      // True when fast-falling
    private bool _isFalling;          // True when falling normally
    private float _fastFallTime;      // Tracks time spent fast-falling
    private float _fastFallReleaseSpeed; // Initial speed when starting fast-fall
    private int _numberOfJumpsUsed;   // Tracks available double jumps

    // Jump apex control (for float effect at jump peak)
    private float _apexPoint;         // Current position in jump arc (0-1)
    private float _timePastApexTreshold;  // Time spent at apex
    private bool _isPastApexTreshold;     // True when at jump peak

    // Jump buffer (allows jump input slightly before landing)
    private float _jumpBufferTimer;   // Time remaining in jump buffer
    private bool _jumpReleasedDuringBuffer; // True if jump released during buffer

    // Coyote time (allows jump slightly after leaving platform)
    private float _coyoteTimer;       // Time remaining for coyote jump

    // Wall slide mechanics
    private bool _isWallSliding;      // True when sliding down wall
    private bool _isWallSlideFalling; // True when falling after wall slide

    // Wall jump state tracking
    private bool _useWallJumpMoveStats;   // True when using wall jump movement values
    private bool _isWallJumping;          // True during wall jump
    private float _wallJumpTime;          // Duration of current wall jump
    private bool _isWallJumpFastFalling;  // True when fast-falling during wall jump
    private bool _isWallJumpFalling;      // True when falling normally after wall jump
    private float _wallJumpFastFallTime;  // Time spent fast-falling during wall jump
    private float _wallJumpFastFallReleaseSpeed; // Initial speed when fast-falling from wall jump
    private float _wallJumpPostBufferTimer;      // Buffer time for wall jump input

    // Wall jump apex control
    private float _wallJumpApexPoint;         // Position in wall jump arc (0-1)
    private float _timePastWallJumpApexTreshold;  // Time at wall jump apex
    private bool _isPastWallJumpApexTreshold;     // True at wall jump peak

    // Dash mechanics
    private bool _isDashing;          // True during dash
    private bool _isAirDashing;       // True when dashing in air
    private float _dashTimer;         // Duration of current dash
    private float _dashOnGroundTimer; // Cooldown for ground dashes
    private int _numberOfDashesUsed;  // Tracks available air dashes
    private Vector2 _dashDirection;   // Current dash direction
    private bool _isDashFastFalling;  // True when fast-falling after dash
    private float _dashFastFallTime;  // Time spent fast-falling after dash
    private float _dashFastFallReleaseSpeed; // Initial speed when fast-falling from dash

    #endregion

#region Start
    void Start()
    {
        // Initialize player facing direction
        _isFacingRight = true;

        // Get required components
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Check various movement states and inputs each frame
        JumpChecks();          // Handle jump input and state
        CountTimers();         // Update all gameplay timers
        LandCheck();           // Check if player has landed
        WallSlideCheck();      // Check wall sliding state
        WallJumpCheck();       // Handle wall jump mechanics
        DashCheck();           // Process dash input and state

        // Animation States
        // Set running animation when moving on ground
        if (Mathf.Abs(InputManager.Movement.x) > 0.1f && _isGrounded)
        {
            _animator.SetBool("IsRunning", true);
        }
        else
        {
            _animator.SetBool("IsRunning", false);
        }

        // Set jumping animation for any upward movement
        _animator.SetBool("IsJumping", _isJumping || _isWallJumping);

        // Set falling animation for any downward movement
        _animator.SetBool("IsFalling", 
            _isFalling || 
            _isFastFalling || 
            _isWallJumpFalling || 
            _isWallJumpFastFalling || 
            _isDashFastFalling || 
            _isWallSlideFalling
        );

        // Set wall sliding animation
        _animator.SetBool("IsWallSliding", _isWallSliding);

        // Set dashing animation
        _animator.SetBool("IsDashing", _isDashing);

        // Set Grounded animation Bool
        _animator.SetBool("IsGround", _isGrounded);

    }

    void FixedUpdate()
    {
        // Physics-based updates that run at fixed time intervals
        CollisionCheck();      // Check for collisions with environment
        Jump();               // Process jump physics
        Fall();               // Handle falling mechanics
        WallSlide();          // Process wall slide physics
        WallJump();           // Handle wall jump physics
        Dash();               // Process dash movement

        // Handle movement with different acceleration values based on state
        if (_isGrounded)
        {
            // Use ground movement values when on ground 
            Move(MoveStats.GroundAcceleration, MoveStats.GroundDeceleration, InputManager.Movement);
        }
        else
        {
            if (_useWallJumpMoveStats)
            {
                // Use special movement values during wall jump
                Move(MoveStats.WallJumpMoveAcceleration, MoveStats.WallJumpMoveDeceleration, InputManager.Movement);
            }
            else
            {
                // Use air movement values when in air
                Move(MoveStats.AirAcceleration, MoveStats.AirDeceleration, InputManager.Movement);
            }
        }

        // Apply final velocity to rigidbody
        ApplyVelocity();
    }

    private void ApplyVelocity()
    {
        // Clamp vertical velocity to prevent excessive speeds
        if (!_isDashing)
        {
            // Normal vertical speed limits
            VerticalVelocity = Mathf.Clamp(VerticalVelocity, -MoveStats.MaxFallSpeed, 50f);
        }
        else
        {
            // Different limits during dash
            VerticalVelocity = Mathf.Clamp(VerticalVelocity, -50, 50f);
        }

        // Apply final velocity to rigidbody
        _rb.linearVelocity = new Vector2(HorizontalVelocity, VerticalVelocity);
    }
    #endregion

#region Movement
    /// <summary>
    /// Handles horizontal movement with acceleration and deceleration
    /// </summary>
    /// <"acceleration">Rate at which player reaches max speed
    /// <"deceleration">Rate at which player slows down
    /// <"moveInput">Raw input direction from player
    private void Move(float acceleration, float deceleration, Vector2 moveInput)
    {
        // Only process movement if not dashing
        if (!_isDashing)
        {
            // Check if input exceeds minimum threshold for movement
            if (Mathf.Abs(moveInput.x) >= MoveStats.MoveTreshold)
            {
                // Check if player needs to turn based on input direction
                TurnCheck(moveInput);
                float targetVelocity = 0f;
                
                // Calculate target velocity based on input direction and max speed
                targetVelocity = moveInput.x * MoveStats.MaxSpeed;

                // Smoothly interpolate current velocity towards target velocity
                HorizontalVelocity = Mathf.Lerp(HorizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            }
            // If input is below threshold, decelerate to stop
            else if (Mathf.Abs(moveInput.x) < MoveStats.MoveTreshold)
            {
                HorizontalVelocity = Mathf.Lerp(HorizontalVelocity, 0f, deceleration * Time.fixedDeltaTime);
            }
        }
    }

    /// <summary>
    /// Checks if player needs to turn based on input direction
    /// </summary>
    private void TurnCheck(Vector2 moveInput)
    {
        // Turn left if facing right and moving left
        if (_isFacingRight && moveInput.x < 0)
        {
            Turn(false);
        }
        // Turn right if facing left and moving right
        else if (!_isFacingRight && moveInput.x > 0)
        {
            Turn(true);
        }
    }

    /// <summary>
    /// Handles player character rotation when changing direction
    /// </summary>
    /// <param name="turnRight">True to turn right, false to turn left</param>
    private void Turn(bool turnRight)
    {
        if (turnRight)
        {
            _isFacingRight = true;
            transform.Rotate(0f, 180f, 0f);  // Rotate character model 180 degrees around Y axis
        }
        else
        {
            _isFacingRight = false;
            transform.Rotate(0f, -180f, 0f); // Rotate character model -180 degrees around Y axis
        }
    }
#endregion

#region Land/Fall
    /// <summary>
    /// Checks landing conditions and resets appropriate movement states
    /// </summary>
    private void LandCheck()
    {
        // Check if player is landing from any aerial state (jumping, falling, wall jumping, etc.)
        if ((_isJumping || _isFalling || _isWallJumpFalling || _isWallJumping || 
             _isWallSlideFalling || _isWallSliding || _isDashFastFalling) && 
             _isGrounded && VerticalVelocity <= 0f)
        {
            // Reset all movement states and abilities
            ResetJumpValues();
            StopWallSlide();
            ResetWallJumpValues();
            ResetDashes();

            // Reset available jumps when landing
            _numberOfJumpsUsed = 0;

            // Apply ground gravity
            VerticalVelocity = Physics2D.gravity.y;

            // Special handling for landing from dash fall
            if (_isDashFastFalling && _isGrounded)
            {
                ResetDashValues();
                return;
            }

            ResetDashValues();
        }
    }

    /// <summary>
    /// Handles falling mechanics and gravity application
    /// </summary>
    private void Fall()
    {
        // Apply normal gravity when falling without any special states
        if (!_isGrounded && !_isJumping && !_isWallJumping && 
            !_isWallSliding && !_isDashing && !_isDashFastFalling)
        {
            // Set falling state if not already falling
            if (!_isFalling)
            {
                _isFalling = true;
                VerticalVelocity = 0f;
            }

            // Apply standard gravity
            VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;

            VerticalVelocity = Mathf.Max(VerticalVelocity, -MoveStats.MaxFallSpeed);
        }
    }
#endregion


#region Jump
    /// <summary>
    /// Resets all jump-related values to their default states
    /// </summary>
    private void ResetJumpValues()
    {
        _isJumping = false;
        _isFalling = false;
        _isFastFalling = false;
        _fastFallTime = 0f;
        _isPastApexTreshold = false;
    }

    /// <summary>
    /// Handles jump input detection and jump state management
    /// </summary>
    private void JumpChecks()
    {
        // JUMP BUTTON PRESSED
        if (InputManager.JumpWasPressed)
        {   
            // Prevent jump if falling from wall slide with active wall jump buffer
            if (_isWallSlideFalling && _wallJumpPostBufferTimer >= 0f)
            {
                return;
            }

            // Prevent normal jump when wall sliding or touching wall in air
            if (_isWallSliding || (_isTouchingWall && !_isGrounded))
            {
                return;
            }

            // Start jump buffer timer and track button release
            _jumpBufferTimer = MoveStats.JumpBufferTime;
            _jumpReleasedDuringBuffer = false;
        }

        // JUMP BUTTON RELEASED
        if (InputManager.JumpWasReleased)
        {
            // Track if jump was released during buffer window
            if (_jumpBufferTimer > 0f)
            {
                _jumpReleasedDuringBuffer = true;
            }

            // Variable jump height when releasing during upward motion
            if (_isJumping && VerticalVelocity > 0f)
            {
                if (_isPastApexTreshold)
                {
                    // Quick fall if past apex threshold
                    _isPastApexTreshold = false;
                    _isFastFalling = true;
                    _fastFallTime = MoveStats.TimeForUpwardsCancel;
                    VerticalVelocity = 0f;
                }
                else
                {
                    // Start fast fall from current position
                    _isFastFalling = true;
                    _fastFallReleaseSpeed = VerticalVelocity;
                }
            }
        }

        // EXECUTE JUMP WITH BUFFER AND COYOTE TIME
        if (_jumpBufferTimer > 0f && !_isJumping && (_isGrounded || _coyoteTimer > 0f))
        {
            InitiateJump(1);

            // Handle early button release during buffer
            if (_jumpReleasedDuringBuffer)
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = VerticalVelocity;
            }
        }

        // DOUBLE JUMP
        if (_jumpBufferTimer > 0f && 
            (_isJumping || _isFalling || _isWallJumping || _isWallSlideFalling || _isAirDashing || _isDashFastFalling) && 
            !_isTouchingWall && 
            _numberOfJumpsUsed < MoveStats.NumberOfJumpsAllowed)
        {
            _isFastFalling = false;
            InitiateJump(1);

            // Reset dash fall state if active
            if (_isDashFastFalling)
            {
                _isDashFastFalling = false;
            }
        }

        // AIR JUMP AFTER COYOTE TIME
        if (_jumpBufferTimer > 0f && !_isFalling && _isWallSlideFalling && 
            _numberOfJumpsUsed < MoveStats.NumberOfJumpsAllowed)
        {
            _isFastFalling = false;
            InitiateJump(1);
        }
    }

    /// <summary>
    /// Starts a new jump with given jump count increment
    /// </summary>
    private void InitiateJump(int jumpsUsed)
    {
        if (!_isJumping)
        {
            _isJumping = true;
        }

        ResetWallJumpValues();
        _jumpBufferTimer = 0f;

        // Track jump count
        _numberOfJumpsUsed += jumpsUsed;
        _numberOfJumpsUsed = Mathf.Clamp(_numberOfJumpsUsed, 0, MoveStats.NumberOfJumpsAllowed);

        // Set initial jump velocity
        VerticalVelocity = MoveStats.InitialJumpVelocity;
    }

    /// <summary>
    /// Handles jump physics and apex control
    /// </summary>
    private void Jump()
    {
        if (_isJumping)
        {
            // Cancel upward momentum on ceiling hit
            if (_bumpedHead)
            {
                _isFastFalling = true;
            }

            // Rising jump phase
            if (VerticalVelocity >= 0f)
            {
                // Calculate position in jump arc
                _apexPoint = Mathf.InverseLerp(MoveStats.InitialJumpVelocity, 0f, VerticalVelocity);

                // Apply apex floating effect
                if (_apexPoint > MoveStats.ApexTreshold)
                {
                    if (!_isPastApexTreshold)
                    {
                        _isPastApexTreshold = true;
                        _timePastApexTreshold = 0f;
                    }

                    // Hang time at apex
                    if (_isPastApexTreshold)
                    {
                        _timePastApexTreshold += Time.fixedDeltaTime;
                        if (_timePastApexTreshold < MoveStats.ApexHangTime)
                        {
                            // Smooth velocity reduction at apex
                            VerticalVelocity = Mathf.Lerp(VerticalVelocity, 0f, Time.fixedDeltaTime * 10f);
                        }
                        else
                        {
                            // Resume gravity after hang time
                            VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;
                        }
                    }
                }
                else
                {
                    // Normal upward gravity
                    VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;
                }
            }
            // Falling phase without fast fall
            else if(!_isFastFalling)
            {
                VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;
                if (_isPastApexTreshold)
                {
                    _isPastApexTreshold = false;
                }
            }
        }
        // Apply increased gravity when falling
        else if (!_isFastFalling)
        {
            VerticalVelocity += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
        }
        // Set falling state
        else if (VerticalVelocity < 0f)
        {
            if (!_isFalling)
            {
                _isFalling = true;
            }
        }

        // Handle fast falling physics
        if (_isFastFalling)
        {
            if (_fastFallTime >= MoveStats.TimeForUpwardsCancel)
            {
                // Apply increased gravity during fast fall
                VerticalVelocity += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_fastFallTime < MoveStats.TimeForUpwardsCancel)
            {
                // Smooth transition to fast fall
                VerticalVelocity = Mathf.Lerp(_fastFallReleaseSpeed, 0f, (_fastFallTime / MoveStats.TimeForUpwardsCancel));
            }

            _fastFallTime += Time.fixedDeltaTime;
        }
    }
#endregion

#region Wall Slide
    /// <summary>
    /// Checks conditions for starting and stopping wall slides
    /// </summary>
    private void WallSlideCheck()
    {
        // Start wall slide when touching wall while falling
        if (_isTouchingWall && !_isGrounded && !_isDashing)
        {
            if (VerticalVelocity < 0f && !_isWallSliding)
            {
                // Reset other movement states when starting wall slide
                ResetJumpValues();
                ResetWallJumpValues();
                ResetDashValues();

                // Optionally reset available dashes
                if (MoveStats.ResetDashOnWallSlide)
                {
                    ResetDashes();
                }

                _isWallSlideFalling = false;
                _isWallSliding = true;

                // Optionally reset available jumps
                if (MoveStats.ResetJumpOnWallSlide)
                {
                    _numberOfJumpsUsed = 0;
                }
            }
        }
        // Handle leaving wall while sliding
        else if (_isWallSliding && !_isTouchingWall && !_isGrounded && !_isWallSlideFalling)
        {
            _isWallSlideFalling = true;
            StopWallSlide();
        }
        else 
        {
            StopWallSlide();
        }
    }

    /// <summary>
    /// Stops wall slide and increments jump counter
    /// </summary>
    private void StopWallSlide()
    {
        if (_isWallSliding)
        {
            _numberOfJumpsUsed++;
            _isWallSliding = false;
        }
    }

    /// <summary>
    /// Applies wall slide physics and velocity
    /// </summary>
    private void WallSlide()
    {
        if (_isWallSliding)
        {
            // Smoothly transition to wall slide speed
            VerticalVelocity = Mathf.Lerp(VerticalVelocity, -MoveStats.WallSlideSpeed, 
                MoveStats.WallSlideDecelerationSpeed * Time.fixedDeltaTime);
        }
    }
#endregion

#region Wall Jump
    /// <summary>
    /// Handles wall jump input detection and state management
    /// </summary>
    private void WallJumpCheck()
    {
        // Start wall jump buffer when touching wall in air
        if (ShouldApplyPostWallJumpBuffer())
        {
            _wallJumpPostBufferTimer = MoveStats.WallJumpPostBufferTime;
        }

        // Handle early button release during wall jump
        if (InputManager.JumpWasReleased && !_isWallSliding && !_isTouchingWall && _isWallJumping)
        {
            if (VerticalVelocity > 0f)
            {
                // Quick fall if past apex threshold
                if (_isPastWallJumpApexTreshold)
                {
                    _isPastWallJumpApexTreshold = false;
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallTime = MoveStats.TimeForUpwardsCancel;
                    VerticalVelocity = 0f;
                }
                else 
                {
                    // Start fast fall from current position
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallReleaseSpeed = VerticalVelocity;
                }
            }
        }

        // Execute wall jump if input occurs during buffer window
        if (InputManager.JumpWasPressed && _wallJumpPostBufferTimer > 0f)
        {
            InitiateWallJump();
        }
    }

    /// <summary>
    /// Initializes wall jump state and sets initial velocities
    /// </summary>
    private void InitiateWallJump()
    {
        // Set wall jump state flags
        if (!_isWallJumping)
        {
            _isWallJumping = true;
            _useWallJumpMoveStats = true;  // Use special movement values during wall jump
        }

        // Reset other movement states
        StopWallSlide();
        ResetJumpValues();
        _wallJumpTime = 0f;

        // Apply initial vertical velocity
        VerticalVelocity = MoveStats.InitialWallJumpVelocity;
        
        // Determine horizontal direction based on wall position
        int dirMultiplier = 0;
        Vector2 hitPoint = _lastWallHit.collider.ClosestPoint(_bodyColl.bounds.center);

        // Jump away from wall
        if (hitPoint.x > transform.position.x)
        {
            dirMultiplier = -1;  // Wall is on right, jump left
        }
        else
        {
            dirMultiplier = 1;   // Wall is on left, jump right
        }

        // Apply horizontal velocity in opposite direction of wall
        HorizontalVelocity = Mathf.Abs(MoveStats.WallJumpDirection.x) * dirMultiplier;
    }

    /// <summary>
    /// Handles wall jump physics and apex control
    /// </summary>
    private void WallJump()
    {
        if (_isWallJumping)
        {
            // Track wall jump duration
            _wallJumpTime += Time.fixedDeltaTime;
            if (_wallJumpTime >= MoveStats.TimeTillJumpApex)
            {
                _useWallJumpMoveStats = false;  // Return to normal air control
            }

            // Cancel upward momentum on ceiling hit
            if (_bumpedHead)
            {
                _isWallJumpFastFalling = true;
                _useWallJumpMoveStats = false;
            }

            // Rising phase of wall jump
            if (VerticalVelocity >= 0f)
            {
                // Calculate position in jump arc
                _wallJumpApexPoint = Mathf.InverseLerp(MoveStats.WallJumpDirection.y, 0f, VerticalVelocity);

                // Apply apex floating effect
                if (_wallJumpApexPoint > MoveStats.ApexTreshold)
                {
                    if (!_isPastWallJumpApexTreshold)
                    {
                        _isPastWallJumpApexTreshold = true;
                        _timePastWallJumpApexTreshold = 0f;
                    }

                    // Hang time at apex
                    if (_isPastWallJumpApexTreshold)
                    {
                        _timePastWallJumpApexTreshold += Time.fixedDeltaTime;
                        if (_timePastWallJumpApexTreshold < MoveStats.ApexHangTime)
                        {
                            VerticalVelocity = 0f;  // Float at apex
                        }
                        else
                        {
                            VerticalVelocity = -0.01f;  // Start falling
                        }
                    }
                }
                // Normal upward phase
                else if (!_isWallJumpFastFalling)
                {
                    VerticalVelocity += MoveStats.WallJumpGravity * Time.fixedDeltaTime;

                    if (_isPastWallJumpApexTreshold)
                    {
                        _isPastWallJumpApexTreshold = false;
                    }
                }
            }
            // Falling phase without fast fall
            else if (!_isWallJumpFastFalling)
            {
                VerticalVelocity += MoveStats.WallJumpGravity * Time.fixedDeltaTime;
            }
            // Set falling state
            else if (VerticalVelocity < 0f)
            {
                if (!_isWallJumpFalling)
                {
                    _isWallJumpFalling = true;
                }
            }
        }

        // Handle fast falling physics after wall jump
        if (_isWallJumpFastFalling)
        {
            if (_wallJumpFastFallTime >= MoveStats.TimeForUpwardsCancel)
            {
                // Apply increased gravity during fast fall
                VerticalVelocity += MoveStats.WallJumpGravity * MoveStats.WallJumpGravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_wallJumpFastFallTime < MoveStats.TimeForUpwardsCancel)
            {
                // Smooth transition to fast fall
                VerticalVelocity = Mathf.Lerp(_wallJumpFastFallReleaseSpeed, 0f, (_wallJumpFastFallTime / MoveStats.TimeForUpwardsCancel));
            }

            _wallJumpFastFallTime += Time.fixedDeltaTime;
        }
    }

    /// <summary>
    /// Checks if wall jump buffer should be active
    /// </summary>
    private bool ShouldApplyPostWallJumpBuffer()
    {
        // Allow buffer when touching wall in air
        if (!_isGrounded && (_isTouchingWall || _isWallSliding))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Resets all wall jump related states
    /// </summary>
    private void ResetWallJumpValues()
    {
        _isWallSlideFalling = false;
        _useWallJumpMoveStats = false;
        _isWallJumping = false;
        _isWallJumpFastFalling = false;
        _isWallJumpFalling = false;
        _isPastWallJumpApexTreshold = false;
        _wallJumpTime = 0f;
        _wallJumpFastFallTime = 0f;
    }
#endregion

#region Dash
    /// <summary>
    /// Handles dash input detection and validation for both ground and air dashes
    /// </summary>
    private void DashCheck()
    {
        if (InputManager.DashWasPressed)
        {
            // Ground dash - Checks cooldown timer before allowing dash
            if (_isGrounded && _dashOnGroundTimer < 0f && !_isDashing)
            {
                InitiateDash();
            }
            // Air dash - Checks remaining air dashes before allowing
            else if (!_isGrounded && !_isDashing && _numberOfDashesUsed < MoveStats.NumberOfDashes)
            {
                _isAirDashing = true;
                InitiateDash();

                // Special case: Handle dash during wall jump buffer window
                if (_wallJumpPostBufferTimer > 0f)
                {
                    // Preserve jump count when dashing from wall
                    _numberOfJumpsUsed--;
                    _numberOfJumpsUsed = Mathf.Max(_numberOfJumpsUsed, 0);
                }
            }
        }
    }

    /// <summary>
    /// Sets up dash direction and initializes dash state
    /// </summary>
    private void InitiateDash()
    {
        // Get raw input direction for dash
        _dashDirection = InputManager.Movement;

        // Find closest predefined dash direction
        Vector2 closestDirection = Vector2.zero;
        float minDistance = Vector2.Distance(_dashDirection, MoveStats.DashDirections[0]);

        // Compare input against all allowed dash directions
        for (int i = 0; i < MoveStats.DashDirections.Length; i++)
        {
            // Exact match found
            if (_dashDirection == MoveStats.DashDirections[i])
            {
                closestDirection = _dashDirection;
                break;
            }

            float distance = Vector2.Distance(_dashDirection, MoveStats.DashDirections[i]);

            // Apply bias to diagonal directions to make them easier to select
            bool isDiagonal = Mathf.Abs(MoveStats.DashDirections[i].x) == 1 && 
                            Mathf.Abs(MoveStats.DashDirections[i].y) == 1;
            if (isDiagonal)
            {
                distance -= MoveStats.DashDiagonallyBias;
            }
            else if (distance < minDistance)
            {
                minDistance = distance;
                closestDirection = MoveStats.DashDirections[i];
            }
        }

        // Default to facing direction if no input given
        if (closestDirection == Vector2.zero)
        {
            closestDirection = _isFacingRight ? Vector2.right : Vector2.left;
        }

        // Initialize dash state and parameters
        _dashDirection = closestDirection;
        _numberOfDashesUsed++;
        _isDashing = true;
        _dashTimer = 0f;
        _dashOnGroundTimer = MoveStats.TimeBtwDashesOnGround;

        // Reset other movement states
        ResetJumpValues();
        ResetWallJumpValues();
        StopWallSlide();
    }
    
    /// <summary>
    /// Handles dash movement and transitions to post-dash states
    /// </summary>
    private void Dash()
    {
        if (_isDashing)
        {
            // Track dash duration
            _dashTimer += Time.fixedDeltaTime;
            if (_dashTimer >= MoveStats.DashTime)
            {
                // Reset dashes if grounded
                if (_isGrounded)
                {
                    ResetDashes();
                }

                // End dash state
                _isAirDashing = false;
                _isDashing = false;

                // Handle transition to falling after air dash
                if (!_isJumping && !_isWallJumping)
                {
                    _dashFastFallTime = 0f;
                    _dashFastFallReleaseSpeed = VerticalVelocity;

                    if (!_isGrounded)
                    {
                        _isDashFastFalling = true;
                    }
                }

                return;
            }

            // Apply dash velocity
            HorizontalVelocity = MoveStats.DashSpeed * _dashDirection.x;

            // Apply vertical velocity only for air dashes or diagonal dashes
            if (_dashDirection.y != 0f || _isAirDashing)
            {
                VerticalVelocity = MoveStats.DashSpeed * _dashDirection.y;
            }
        }
        // Handle fast falling after dash ends
        else if (_isDashFastFalling)
        {
            if (VerticalVelocity > 0f)
            {
                // Smooth transition to falling
                if (_dashFastFallTime < MoveStats.DashTimeForUpwardsCancel)
                {
                    VerticalVelocity = Mathf.Lerp(_dashFastFallReleaseSpeed, 0f, 
                        (_dashFastFallTime / MoveStats.DashTimeForUpwardsCancel));
                }
                else
                {
                    // Apply increased gravity
                    VerticalVelocity += MoveStats.Gravity * MoveStats.DashGravityOnReleaseMultiplier * Time.fixedDeltaTime;
                }

                _dashFastFallTime += Time.fixedDeltaTime;
            }
            else 
            {
                // Continue applying increased gravity while falling
                VerticalVelocity += MoveStats.Gravity * MoveStats.DashGravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
        }
    }

    /// <summary>
    /// Resets dash-related states
    /// </summary>
    private void ResetDashValues()
    {
        _isDashFastFalling = false;
        _dashOnGroundTimer = 0.01f;  // Small buffer before next ground dash
    }

    /// <summary>
    /// Resets available air dashes
    /// </summary>
    private void ResetDashes()
    {
        _numberOfDashesUsed = 0;
    }
#endregion

#region Timers
    /// <summary>
    /// Updates all gameplay timing variables used for movement mechanics
    /// </summary>
    private void CountTimers()
    {
        // Decrease jump buffer timer (allows jump input before landing)
        _jumpBufferTimer -= Time.deltaTime;

        // Handle coyote time (allows jump shortly after leaving ground)
        if (!_isGrounded)
        {
            _coyoteTimer -= Time.deltaTime;
        }
        else 
        {
            _coyoteTimer = MoveStats.JumpCoyoteTime;  // Reset coyote time when grounded
        }

        // Update wall jump buffer timer
        if (!ShouldApplyPostWallJumpBuffer())
        {
            _wallJumpPostBufferTimer -= Time.deltaTime;
        }

        // Update ground dash cooldown
        if (_isGrounded)
        {
            _dashOnGroundTimer -= Time.deltaTime;
        }
    }
#endregion

#region Collisions Checks
    /// <summary>
    /// Checks if the player is touching the ground using a box cast
    /// </summary>
    private void IsGrounded()
    {
        // Create box cast dimensions based on feet collider
        Vector2 boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _feetColl.bounds.min.y);
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, MoveStats.GroundDetectionRayLenght);

        // Cast box downward to detect ground
        _groundHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.down, MoveStats.GroundDetectionRayLenght, MoveStats.GroundLayer);
        if (_groundHit.collider != null)
        {
            _isGrounded = true;
        }
        else
        {
            _isGrounded = false;
        }
    }

    /// <summary>
    /// Checks if the player's head collides with a ceiling
    /// </summary>
    private void BumpedHead()
    {
        // Create box cast dimensions based on body collider top
        Vector2 boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _bodyColl.bounds.max.y);
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, MoveStats.HeadDetectionRayLenght);

        // Cast box upward to detect ceiling
        _headHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.up, MoveStats.HeadDetectionRayLenght, MoveStats.GroundLayer);
        if (_headHit.collider != null)
        {
            _bumpedHead = true;
        }
        else
        {
            _bumpedHead = false;
        }
    }

    /// <summary>
    /// Checks if the player is touching a wall for wall sliding and jumping
    /// </summary>
    private void IsTouchingWall()
    {
        // Determine ray origin based on facing direction
        float originEndPoint = 0f;
        if (_isFacingRight)
        {
            originEndPoint = _bodyColl.bounds.max.x;  // Use right edge when facing right
        }
        else
        {
            originEndPoint = _bodyColl.bounds.min.x;  // Use left edge when facing left
        }

        // Adjust height of wall detection box
        float adjustedHeight = _bodyColl.bounds.size.y * MoveStats.WallDetectionRayHeightMultiplier;

        // Create box cast dimensions
        Vector2 boxCastOrigin = new Vector2(originEndPoint, _bodyColl.bounds.center.y);
        Vector2 boxCastSize = new Vector2(MoveStats.WallDetectionRayLenght, adjustedHeight);

        // Cast box horizontally to detect walls
        _wallHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, transform.right, MoveStats.WallDetectionRayLenght, MoveStats.GroundLayer);
        
        if (_wallHit.collider != null)
        {
            _lastWallHit = _wallHit;  // Store last wall hit for wall jump direction
            _isTouchingWall = true;
        }
        else
        {
            _isTouchingWall = false;
        }
    }

    /// <summary>
    /// Performs all collision checks in fixed update
    /// </summary>
    private void CollisionCheck()
    {
        IsGrounded();      // Check ground contact
        BumpedHead();      // Check ceiling contact
        IsTouchingWall();  // Check wall contact
    }
#endregion
}
