using Unity.Mathematics;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

#region Variables
    [Header("References")]
    public PlayerMovementStats MoveStats;
    [SerializeField] private Collider2D _feetColl;
    [SerializeField] private Collider2D _bodyColl;

    private Rigidbody2D _rb;
    private Animator _animator; // Add this variable to reference the Animator

    //Variables de movimiento
    public float HorizontalVelocity { get; private set; }
    private bool _isFacingRight;

    // Variables de check de colision
    private RaycastHit2D _groundHit;
    private RaycastHit2D _headHit;
    private RaycastHit2D _wallHit;
    private RaycastHit2D _lastWallHit;
    private bool _isGrounded;
    private bool _bumpedHead;
    private bool _isTouchingWall;

    //Variables de salto
    public float VerticalVelocity { get; private set; }
    private bool _isJumping;
    private bool _isFastFalling;
    private bool _isFalling;
    private float _fastFallTime;
    private float _fastFallReleaseSpeed;
    private int _numberOfJumpsUsed;

    //Variables de Apex
    private float _apexPoint;
    private float _timePastApexTreshold;
    private bool _isPastApexTreshold;

    //Variables de buffer de salto
    private float _jumpBufferTimer;
    private bool _jumpReleasedDuringBuffer;

    //Variables de Coyote Time
    private float _coyoteTimer;

    //Variables de Wall Slide
    private bool _isWallSliding;
    private bool _isWallSlideFalling;

    //Variables de Wall Jump
    private bool _useWallJumpMoveStats;
    private bool _isWallJumping;
    private float _wallJumpTime;
    private bool _isWallJumpFastFalling;
    private bool _isWallJumpFalling;
    private float _wallJumpFastFallTime;
    private float _wallJumpFastFallReleaseSpeed;

    private float _wallJumpPostBufferTimer;

    private float _wallJumpApexPoint;
    private float _timePastWallJumpApexTreshold;
    private bool _isPastWallJumpApexTreshold;

    //Variables de Dash
    private bool _isDashing;
    private bool _isAirDashing;
    private float _dashTimer;
    private float _dashOnGroundTimer;
    private int _numberOfDashesUsed;
    private Vector2 _dashDirection;
    private bool _isDashFastFalling;
    private float _dashFastFallTime;
    private float _dashFastFallReleaseSpeed; 

    #endregion

#region Start
     void Start()
   
    {
        _isFacingRight = true;

        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>(); // Reference the Animator component
    }

    void Update()
    {
        JumpChecks();
        CountTimers();
        LandCheck();
        WallSlideCheck();
        WallJumpCheck();
        DashCheck();

        // Check if the move input is pressed
        if (Mathf.Abs(InputManager.Movement.x) > 0.1f && _isGrounded)
        {
            _animator.SetBool("IsRunning", true); 
        }
        else
        {
            _animator.SetBool("IsRunning", false); 
        }

        _animator.SetBool("IsJumping", _isJumping || _isWallJumping); 
        _animator.SetBool("IsFalling", _isFalling || _isFastFalling || _isWallJumpFalling || _isWallJumpFastFalling || _isDashFastFalling || _isWallSlideFalling); 
    }

    void FixedUpdate()
    {
        CollisionCheck();
        Jump();
        Fall();
        WallSlide();
        WallJump();
        Dash();

        if (_isGrounded)
        {
            Move(MoveStats.GroundAcceleration, MoveStats.GroundDeceleration, InputManager.Movement);
        }
        else
        {
            if (_useWallJumpMoveStats)
            {
                Move(MoveStats.WallJumpMoveAcceleration, MoveStats.WallJumpMoveDeceleration, InputManager.Movement);
            }
            else
            {
                Move(MoveStats.AirAcceleration, MoveStats.AirDeceleration, InputManager.Movement);
            }

        }

        ApplyVelocity();
    }

    private void ApplyVelocity()
    {
        if (!_isDashing)
        {
            VerticalVelocity = Mathf.Clamp(VerticalVelocity, -MoveStats.MaxFallSpeed, 50f);
        }
        else
        {
            VerticalVelocity = Mathf.Clamp(VerticalVelocity, -50, 50f);
        }

        _rb.linearVelocity = new Vector2(HorizontalVelocity, VerticalVelocity);
    }
    #endregion

#region Movement
        private void Move(float acceleration, float deceleration, Vector2 moveInput)
    {
        if (!_isDashing)
        {
            if (Mathf.Abs(moveInput.x) >= MoveStats.MoveTreshold)
            {
                TurnCheck(moveInput);
                float targetVelocity =0f;
                
                targetVelocity = moveInput.x * MoveStats.MaxSpeed;

                HorizontalVelocity = Mathf.Lerp(HorizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            }
            
            else if (Mathf.Abs(moveInput.x) < MoveStats.MoveTreshold)
            {
                HorizontalVelocity = Mathf.Lerp(HorizontalVelocity, 0f, deceleration * Time.fixedDeltaTime);
            }
        }
        

        
    }

    private void TurnCheck(Vector2 moveInput)
    {
        if (_isFacingRight && moveInput.x < 0)
        {
            Turn(false);
        }

        else if (!_isFacingRight && moveInput.x > 0)
        {
            Turn(true);
        }
    }

    private void Turn(bool turnRight)
    {
        if (turnRight)
        {
            _isFacingRight = true;
            transform.Rotate(0f, 180f, 0f);
        }
        else
        {
            _isFacingRight = false;
            transform.Rotate(0f, -180f, 0f);
        }
    }

    #endregion

#region Land/Fall

    private void LandCheck()
    {
        // AL CAER
        if ((_isJumping || _isFalling || _isWallJumpFalling || _isWallJumping || _isWallSlideFalling || _isWallSliding || _isDashFastFalling) && _isGrounded && VerticalVelocity <= 0f)
        {
            ResetJumpValues();
            StopWallSlide();
            ResetWallJumpValues();
            ResetDashes();

            _numberOfJumpsUsed = 0;

            VerticalVelocity = Physics2D.gravity.y;

            if (_isDashFastFalling && _isGrounded)
            {
                ResetDashValues();
                return;
            }

            ResetDashValues();
        }
    }

    private void Fall()
    {
         //GRAVEDAD NORMAL AL CAER
        if (!_isGrounded && !_isJumping && !_isWallJumping && !_isWallSliding && !_isDashing && !_isDashFastFalling)
        {
            if (!_isFalling)
            {
                _isFalling = true;
            }

            VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;
        }
    }

    #endregion

#region Jump

    private void ResetJumpValues()
    {
        _isJumping = false;
        _isFalling = false;
        _isFastFalling = false;
        _fastFallTime = 0f;
        _isPastApexTreshold = false;
    }

    private void JumpChecks()
    {
        // WHEN JUMP IS PRESSED
        if (InputManager.JumpWasPressed)
        {   
            if (_isWallSlideFalling && _wallJumpPostBufferTimer >= 0f)
            {
                return;
            }

            // Allow jumping if grounded, even when touching a wall
            if (_isWallSliding || (_isTouchingWall && !_isGrounded))
            {
                return;
            }

            _jumpBufferTimer = MoveStats.JumpBufferTime;
            _jumpReleasedDuringBuffer = false;
        }

        // WHEN JUMP IS RELEASED
        if (InputManager.JumpWasReleased)
        {
            if (_jumpBufferTimer > 0f)
            {
                _jumpReleasedDuringBuffer = true;
            }

            if (_isJumping && VerticalVelocity > 0f)
            {
                if (_isPastApexTreshold)
                {
                    _isPastApexTreshold = false;
                    _isFastFalling = true;
                    _fastFallTime = MoveStats.TimeForUpwardsCancel;
                    VerticalVelocity = 0f;
                }
                else
                {
                    _isFastFalling = true;
                    _fastFallReleaseSpeed = VerticalVelocity;
                }
            }
        }

        // START JUMP WITH BUFFERING AND COYOTE TIME
        if (_jumpBufferTimer > 0f && !_isJumping && (_isGrounded || _coyoteTimer > 0f))
        {
            InitiateJump(1);

            if (_jumpReleasedDuringBuffer)
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = VerticalVelocity;
            }
        }

        // DOUBLE JUMP
        if (_jumpBufferTimer > 0f && (_isJumping || _isWallJumping || _isWallSlideFalling || _isAirDashing || _isDashFastFalling) && !_isTouchingWall && _numberOfJumpsUsed < MoveStats.NumberOfJumpsAllowed)
        {
            _isFastFalling = false;
            InitiateJump(1);

            if (_isDashFastFalling)
            {
                _isDashFastFalling = false;
            }
        }

        // AIR JUMP AFTER COYOTE TIME
        if (_jumpBufferTimer > 0f && !_isFalling && _isWallSlideFalling && _numberOfJumpsUsed < MoveStats.NumberOfJumpsAllowed)
        {
            _isFastFalling = false;
            InitiateJump(1);
        }
    }

    private void InitiateJump(int jumpsUsed)
    {
        if (!_isJumping)
        {
            _isJumping = true;
        }

        ResetWallJumpValues();

        _jumpBufferTimer = 0f;

        // Increment the number of jumps used
        _numberOfJumpsUsed += jumpsUsed;

        // Ensure the jump count does not exceed the allowed limit
        _numberOfJumpsUsed = Mathf.Clamp(_numberOfJumpsUsed, 0, MoveStats.NumberOfJumpsAllowed);

        VerticalVelocity = MoveStats.InitialJumpVelocity;
    }

    private void Jump()
    {
        //APLICAR GRAVEDAD AL SALTAR
        if (_isJumping)
        {
            //CHEKEAR SI TOCAMOS TECHO
            if (_bumpedHead)
            {
                _isFastFalling = true;
            }
            //GRAVEDAD AL ASCENDER
            if (VerticalVelocity >= 0f)
            {
                //CONTROL DEL APEX
                _apexPoint = Mathf.InverseLerp(MoveStats.InitialJumpVelocity, 0f, VerticalVelocity);

                if (_apexPoint > MoveStats.ApexTreshold)
                {
                    if (!_isPastApexTreshold)
                    {
                        _isPastApexTreshold = true;
                        _timePastApexTreshold = 0f;
                    }

                    if (_isPastApexTreshold)
                    {
                        _timePastApexTreshold += Time.fixedDeltaTime;
                        if (_timePastApexTreshold < MoveStats.ApexHangTime)
                        {
                            VerticalVelocity = Mathf.Lerp(VerticalVelocity, 0f, Time.fixedDeltaTime * 10f); // Smoothly reduce velocity
                        }
                        else
                        {
                            VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime; // Apply gravity after apex hang
                        }
                    }
                }
                else
                {
                    VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime; // Apply gravity normally
                }
            }
            //GRAVEDAD AL ASCENDER SIN APEX TRESHHOLD
            else if(!_isFastFalling)
            {
                VerticalVelocity += MoveStats.Gravity * Time.fixedDeltaTime;
                if (_isPastApexTreshold)
                {
                    _isPastApexTreshold = false;
                }
            }
        }

        //GRAVEDAD AL DESCENDER
        else if (!_isFastFalling)
        {
            VerticalVelocity += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
        }

        else if (VerticalVelocity < 0f)
        {
            if (!_isFalling)
            {
                _isFalling = true;
            }
        }

        //CORTAR EL SALTO

        if (_isFastFalling)
        {
            if (_fastFallTime >= MoveStats.TimeForUpwardsCancel)
            {
                VerticalVelocity += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_fastFallTime < MoveStats.TimeForUpwardsCancel)
            {
                VerticalVelocity = Mathf.Lerp(_fastFallReleaseSpeed, 0f, (_fastFallTime / MoveStats.TimeForUpwardsCancel));
            }

            _fastFallTime += Time.fixedDeltaTime;
        }

    }

    #endregion

#region Wall Slide
    

    private void WallSlideCheck()
    {
        if (_isTouchingWall && !_isGrounded && !_isDashing)
        {
            if (VerticalVelocity < 0f && !_isWallSliding)
            {
                ResetJumpValues();
                ResetWallJumpValues();
                ResetDashValues();

                if (MoveStats.ResetDashOnWallSlide)
                {
                    ResetDashes();
                }

                _isWallSlideFalling = false;
                _isWallSliding = true;

                if (MoveStats.ResetJumpOnWallSlide)
                {
                    _numberOfJumpsUsed = 0;
                }
            }
        }
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

    private void StopWallSlide()
    {
        if (_isWallSliding)
        {
            _numberOfJumpsUsed++;
            _isWallSliding = false;
        }
    }

    private void WallSlide()
    {
        if (_isWallSliding)
        {
            VerticalVelocity = Mathf.Lerp(VerticalVelocity, -MoveStats.WallSlideSpeed, MoveStats.WallSlideDecelerationSpeed * Time.fixedDeltaTime);
        }
    }

#endregion

#region Wall Jump

    private void WallJumpCheck()
    {
        if (ShouldApplyPostWallJumpBuffer())
        {
            _wallJumpPostBufferTimer = MoveStats.WallJumpPostBufferTime;
        }

        if (InputManager.JumpWasReleased && !_isWallSliding && !_isTouchingWall && _isWallJumping)
        {
            if (VerticalVelocity > 0f)
            {
                if (_isPastWallJumpApexTreshold)
                {
                    _isPastWallJumpApexTreshold = false;
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallTime = MoveStats.TimeForUpwardsCancel;

                    VerticalVelocity = 0f;
                }
                else 
                {
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallReleaseSpeed = VerticalVelocity;
                }

            }
        }

        if (InputManager.JumpWasPressed && _wallJumpPostBufferTimer > 0f)
        {
            InitiateWallJump();
        }
    }

    private void InitiateWallJump()
    {
        if (!_isWallJumping)
        {
            _isWallJumping = true;
            _useWallJumpMoveStats = true;
        }

        StopWallSlide();
        ResetJumpValues();
        _wallJumpTime = 0f;

        VerticalVelocity = MoveStats.InitialWallJumpVelocity;
        
        int dirMultiplier = 0;
        Vector2 hitPoint = _lastWallHit.collider.ClosestPoint(_bodyColl.bounds.center);

        if (hitPoint.x > transform.position.x)
        {
            dirMultiplier = -1;
        }
        else
        {
            dirMultiplier = 1;
        }

        HorizontalVelocity = Mathf.Abs(MoveStats.WallJumpDirection.x) * dirMultiplier;
    }

    private void WallJump()
    {
        if (_isWallJumping)
        {
            _wallJumpTime += Time.fixedDeltaTime;
            if (_wallJumpTime >= MoveStats.TimeTillJumpApex)
            {
                _useWallJumpMoveStats = false;
            }

            if (_bumpedHead)
            {
                _isWallJumpFastFalling = true;
                _useWallJumpMoveStats = false;
            }

            if (VerticalVelocity >= 0f)
            {
                _wallJumpApexPoint = Mathf.InverseLerp(MoveStats.WallJumpDirection.y, 0f, VerticalVelocity);

                if (_wallJumpApexPoint > MoveStats.ApexTreshold)
                {
                    if (!_isPastWallJumpApexTreshold)
                    {
                        _isPastWallJumpApexTreshold = true;
                        _timePastWallJumpApexTreshold = 0f;
                    }

                    if (_isPastWallJumpApexTreshold)
                    {
                        _timePastWallJumpApexTreshold += Time.fixedDeltaTime;
                        if (_timePastWallJumpApexTreshold < MoveStats.ApexHangTime)
                        {
                            VerticalVelocity = 0f;
                        }
                        else
                        {
                            VerticalVelocity = -0.01f;
                        }
                    }
                }

                else if (!_isWallJumpFastFalling)
                {
                    VerticalVelocity += MoveStats.WallJumpGravity * Time.fixedDeltaTime;

                    if (_isPastWallJumpApexTreshold)
                    {
                        _isPastWallJumpApexTreshold = false;
                    }
                }
            }

            else if (!_isWallJumpFastFalling)
            {
                VerticalVelocity += MoveStats.WallJumpGravity * Time.fixedDeltaTime;
            }

            else if (VerticalVelocity < 0f)
            {
                if (!_isWallJumpFalling)
                {
                    _isWallJumpFalling = true;
                }
            }
        }

        if (_isWallJumpFastFalling)
        {
            if (_wallJumpFastFallTime >= MoveStats.TimeForUpwardsCancel)
            {
                VerticalVelocity += MoveStats.WallJumpGravity * MoveStats.WallJumpGravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_wallJumpFastFallTime < MoveStats.TimeForUpwardsCancel)
            {
                VerticalVelocity = Mathf.Lerp(_wallJumpFastFallReleaseSpeed, 0f, (_wallJumpFastFallTime / MoveStats.TimeForUpwardsCancel));
            }

            _wallJumpFastFallTime += Time.fixedDeltaTime;
        }
    }

    private bool ShouldApplyPostWallJumpBuffer()
    {
        if (!_isGrounded && (_isTouchingWall || _isWallSliding))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

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

    private void DashCheck()
    {
        if (InputManager.DashWasPressed)
        {
            //DASH EN EL PISO

            if (_isGrounded && _dashOnGroundTimer < 0f && !_isDashing)
            {
                InitiateDash();
            }
            //DASH EN EL AIRE
            else if (!_isGrounded && !_isDashing && _numberOfDashesUsed < MoveStats.NumberOfDashes)
            {
                _isAirDashing = true;
                InitiateDash();

                //AL SALIR DE UN WALL SLIDE DENTRO DEL WALL JUMP BUFFER
                if (_wallJumpPostBufferTimer > 0f)
                {
                    _numberOfJumpsUsed--;
                    if (_numberOfJumpsUsed < 0f)
                    {
                        _numberOfJumpsUsed = 0;
                    }
                }
            }
        }
    }

    private void InitiateDash()
    {
        _dashDirection = InputManager.Movement;

        Vector2 closestDirection = Vector2.zero;
        float minDistance = Vector2.Distance(_dashDirection, MoveStats.DashDirections[0]);

        for (int i = 0; i < MoveStats.DashDirections.Length; i++)
        {
            if (_dashDirection == MoveStats.DashDirections[i])
            {
                closestDirection = _dashDirection;
                break;
            }

            float distance = Vector2.Distance(_dashDirection, MoveStats.DashDirections[i]);

            bool isDiagonal = Mathf.Abs(MoveStats.DashDirections[i].x) == 1 && Mathf.Abs(MoveStats.DashDirections[i].y) == 1;
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

        if (closestDirection == Vector2.zero)
        {
            if (_isFacingRight)
            {
                closestDirection = Vector2.right;
            }
            else
            {
                closestDirection = Vector2.left;
            }
        }

        _dashDirection = closestDirection;
        _numberOfDashesUsed++;
        _isDashing = true;
        _dashTimer = 0f;
        _dashOnGroundTimer = MoveStats.TimeBtwDashesOnGround;

        ResetJumpValues();
        ResetWallJumpValues();
        StopWallSlide();
    }
    
    private void Dash()
    {
        if (_isDashing)
        {
            _dashTimer += Time.fixedDeltaTime;
            if (_dashTimer >= MoveStats.DashTime)
            {
                if (_isGrounded)
                {
                    ResetDashes();
                }

                _isAirDashing = false;
                _isDashing = false;

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

            HorizontalVelocity = MoveStats.DashSpeed * _dashDirection.x;

            if (_dashDirection.y != 0f || _isAirDashing)
            {
                VerticalVelocity = MoveStats.DashSpeed * _dashDirection.y;
            }
        }

        else if (_isDashFastFalling)
        {
            if (VerticalVelocity > 0f)
            {
                if (_dashFastFallTime < MoveStats.DashTimeForUpwardsCancel)
                {
                    VerticalVelocity = Mathf.Lerp(_dashFastFallReleaseSpeed, 0f, (_dashFastFallTime / MoveStats.DashTimeForUpwardsCancel));
                }
                else
                {
                    VerticalVelocity += MoveStats.Gravity * MoveStats.DashGravityOnReleaseMultiplier * Time.fixedDeltaTime;
                }

                _dashFastFallTime += Time.fixedDeltaTime;
            }

            else 
            {
                VerticalVelocity += MoveStats.Gravity * MoveStats.DashGravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
        }
    }


    private void ResetDashValues()
    {
        _isDashFastFalling = false;
        _dashOnGroundTimer = 0.01f;
    }

    private void ResetDashes()
    {
        _numberOfDashesUsed = 0;
    }

#endregion

#region Timers

    private void CountTimers()
    {
        _jumpBufferTimer -= Time.deltaTime;

        if (!_isGrounded)
        {
            _coyoteTimer -= Time.deltaTime;
        }
        else 
        {
            _coyoteTimer = MoveStats.JumpCoyoteTime;
        }

        if (!ShouldApplyPostWallJumpBuffer())
        {
            _wallJumpPostBufferTimer -= Time.deltaTime;
        }

        if (_isGrounded)
        {
            _dashOnGroundTimer -= Time.deltaTime;
        }
    }

    #endregion

#region Collisions Checks
    private void IsGrounded()
    {
        Vector2 boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _feetColl.bounds.min.y);
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, MoveStats.GroundDetectionRayLenght);

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

    private void BumpedHead()
    {
        Vector2 boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _bodyColl.bounds.max.y);
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, MoveStats.HeadDetectionRayLenght);

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

    private void IsTouchingWall()
    {
        float originEndPoint = 0f;
        if (_isFacingRight)
        {
            originEndPoint = _bodyColl.bounds.max.x;
        }
        else
        {
            originEndPoint = _bodyColl.bounds.min.x;
        }

        float adjustedHeight = _bodyColl.bounds.size.y * MoveStats.WallDetectionRayHeightMultiplier;

        Vector2 boxCastOrigin = new Vector2(originEndPoint, _bodyColl.bounds.center.y);
        Vector2 boxCastSize = new Vector2(MoveStats.WallDetectionRayLenght, adjustedHeight);

        _wallHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, transform.right, MoveStats.WallDetectionRayLenght, MoveStats.GroundLayer);
        
        if (_wallHit.collider != null)
        {
            _lastWallHit = _wallHit;
            _isTouchingWall = true;
        }
        else
        {
            _isTouchingWall = false;
        }
    }

    private void CollisionCheck()
    {
        IsGrounded();
        BumpedHead();
        IsTouchingWall();
    }
    #endregion

}
