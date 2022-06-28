using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MovementController : NetworkBehaviour
{
    // movement
    public CharacterController characterController;
    public float runSpeed = 10f;

    // animation
    public enum AnimationState 
    {
        Idle,
        Walking,
        Running,
        Jumping,
    }
    public Animator animator;
    public NetworkVariable<AnimationState> Animation = new NetworkVariable<AnimationState>(AnimationState.Idle);
    public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
	public NetworkVariable<Quaternion> Rotation = new NetworkVariable<Quaternion>();


    private float x = 0f;
    private float z = 0f;
    private float rotationFactorPerFrame = 15f;
    private bool isGrounded = true; // only jump animation is implemented, jump physics tbd
    

    // Start is called before the first frame update
    void Start()
    {
        animator.SetTrigger($"{Animation.Value}");
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsLocalPlayer) {
            characterController.transform.position = Position.Value;
            characterController.transform.rotation = Rotation.Value;
            animator.SetTrigger($"{Animation.Value}");
            return;
        }

        handleInput();
    }

    void FixedUpdate()
    {
        if (!IsLocalPlayer) return;

        handleMovement();
        handleRotation();
        handleAnimation();
    }

    #region client handlers -> perform client update and send value to network
    void handleInput()
    {
        x = Input.GetAxisRaw("Horizontal") * runSpeed;
        z = Input.GetAxisRaw("Vertical") * runSpeed;
        isGrounded = !Input.GetKey(KeyCode.Space);
    }

    void handleMovement() 
    {
        Vector3 moveVector = new Vector3(x, 0, z);
        characterController.Move(moveVector * Time.deltaTime);

        MoveServerRpc(characterController.transform.position);
    }
    void handleRotation() 
    {
        Vector3 positionToLookAt;
        positionToLookAt.x = x;
        positionToLookAt.y = 0f;
        positionToLookAt.z = z;

        Quaternion currentRotation = transform.rotation;

        if (x != 0f || z != 0f) 
        {
            Quaternion targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationFactorPerFrame * Time.deltaTime);
        }

        RotateServerRpc(characterController.transform.rotation);
    }

    void handleAnimation() 
    {
        if (!isGrounded)
            setAnimatorTrigger(AnimationState.Jumping);
        else if (x != 0 || z != 0)
            setAnimatorTrigger(AnimationState.Running);
        else 
            setAnimatorTrigger(AnimationState.Idle);
        
    }
    #endregion client handlers
    
    void setAnimatorTrigger(AnimationState animation) // helper function
    {
        animator.SetTrigger($"{animation}");
        AnimateServerRpc(animation);
    }

    #region server rpcs
    [ServerRpc] 
    void MoveServerRpc(Vector3 position) 
    {
        Position.Value = position;
    }

    [ServerRpc]
    void RotateServerRpc(Quaternion rotation)
    {
        Rotation.Value = rotation;
    }

    [ServerRpc]
    void AnimateServerRpc(AnimationState animation) 
    {
        Animation.Value = animation;
    }
    #endregion
}

