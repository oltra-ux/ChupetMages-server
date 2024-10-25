using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;
using Utilities;
using System;
using Unity.Netcode.Components;
using Kart;

//Network variables should be value objects
public struct InputPayload : INetworkSerializable {
    public int tick;
    public DateTime timestamp;
    public ulong networkObjectId;
    public Vector3 inputVector;
    public Vector3 position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter{
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref timestamp);
        serializer.SerializeValue(ref networkObjectId);
        serializer.SerializeValue(ref inputVector);
        serializer.SerializeValue(ref position);
    }
}

public struct StatePayload : INetworkSerializable{
    public int tick;
    public ulong networkObjectId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter{
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref networkObjectId);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref rotation);
        serializer.SerializeValue(ref velocity);
        serializer.SerializeValue(ref angularVelocity);
    }
}



public class PlayerMovement : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] InputReader input;
    ClientNetworkTransform clientNetworkTransform;

    float playerHeight = 2f;

    [SerializeField] Transform orientation;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float movementMultiplier = 10f;
    [SerializeField] float airMultiplier= 0.4f;
   

    [Header("Jumping")]
    public float jumpForce=5f;

    [Header("Keybinds")]
    [SerializeField] KeyCode jumpKey = KeyCode.Space;

    [Header("Drag")]
    float groundDrag = 6f;
    float airDrag = 1f;

    float horizontalMovement;
    float verticalMovement;

    [Header("GroundCheck")]
    [SerializeField] LayerMask groundMask;
    bool isGrounded;
    float groundDistance = 0.4f;

    Vector3 moveDirection;
    Vector3 slopeMoveDirection;

    Rigidbody rb;

    RaycastHit slopeHit;
    
    [Header("Network General")]
    
    NetworkTimer timer;
    const float k_serverTickRate = 60f;
    const int k_bufferSize = 1024;

    [Header("Network Client")]
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;

    [Header("Network Server")]
    CircularBuffer<StatePayload> serverStateBuffer;
    Queue<InputPayload> serverInputQueue;
    [SerializeField] float reconciliationThreshold = 10f;

    [Header("Netcode")]
    [SerializeField] float reconciliationCooldownTime = 1f;
    [SerializeField] float extrapolationLimit = 0.5f; //500ms
    [SerializeField] float extrapolationMultiplier = 1.2f;
    [SerializeField] GameObject serverCube;
    [SerializeField] GameObject clientCube;

    StatePayload extrapolationState;
    CountdownTimer extrapolationCooldown;

    CountdownTimer reconciliationCooldown;


    private void Awake()
    {
        //Physics.simulationMode= SimulationMode.Script;
        timer = new NetworkTimer(k_serverTickRate);
        clientStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
        clientInputBuffer = new CircularBuffer<InputPayload>(k_bufferSize);

        serverStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
        serverInputQueue = new Queue<InputPayload>();

        reconciliationCooldown = new CountdownTimer(reconciliationCooldownTime);
        clientNetworkTransform = GetComponent<ClientNetworkTransform>();
        extrapolationCooldown = new CountdownTimer(extrapolationLimit);

        reconciliationCooldown.OnTimerStart += () => 
        {
            extrapolationCooldown.Stop();
        };
        extrapolationCooldown.OnTimerStart += () =>
        { 
            reconciliationCooldown.Stop();
            SwitchAuthorityMode(AuthorityMode.Server); 
            clientNetworkTransform.authorityMode = AuthorityMode.Server;
        };

        extrapolationCooldown.OnTimerStop += () =>
        {
            extrapolationState = default;
            SwitchAuthorityMode(AuthorityMode.Client);
            clientNetworkTransform.authorityMode = AuthorityMode.Client;
        };
    }

    void SwitchAuthorityMode(AuthorityMode mode){
        clientNetworkTransform.authorityMode = mode;
        bool shouldSync = mode == AuthorityMode.Client;
        //clientNetworkTransform.SyncPositionX = shouldSync;
        //clientNetworkTransform.SyncPositionY = shouldSync;
        //clientNetworkTransform.SyncPositionZ = shouldSync;
    }
    
    private bool OnSlope()
    {
        if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight / 2 + 0.5f))
        {
            if(slopeHit.normal != Vector3.up)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        return false;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        input.Enable();
    }

    private void Update()
    {
        //Debug.Log(rb.velocity.magnitude);
        timer.Update(Time.deltaTime);
        reconciliationCooldown.Tick(Time.deltaTime);
        extrapolationCooldown.Tick(Time.deltaTime);

        isGrounded = Physics.CheckSphere(transform.position - new Vector3(0, 1, 0),groundDistance, groundMask);
        ControlDrag();
        if(IsOwner){
            if(Input.GetKeyDown(jumpKey) && isGrounded)
            {
                Jump();
            }
            if(Input.GetKeyDown(KeyCode.Q))
            {
                transform.position += transform.forward * 20000f;
            }
        }
        Extrapolate();
    }

    void Jump()
    {
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    void ControlDrag()
    {
        if(isGrounded)
        {
            rb.drag = groundDrag;
        }
        else
        {
            rb.drag = airDrag;
        }
    }

    private void FixedUpdate()
    {
        while(timer.ShouldTick()){
            HandleClientTick();
            HandleServerTick();
            //Physics.Simulate(Time.fixedDeltaTime);
        }
        Extrapolate();
    }

    #region RPCs

    void HandleServerTick(){
        if(!IsServer) return;
        //Debug.Log("server Tick");
        var bufferIndex = -1;
        InputPayload inputPayload = default;
        while (serverInputQueue.Count >0) {
            inputPayload = serverInputQueue.Dequeue();
            bufferIndex = inputPayload.tick % k_bufferSize;

            StatePayload statePayload = ProcessMovement(inputPayload);
            serverCube.transform.position = statePayload.position;
            serverStateBuffer.Add(statePayload, bufferIndex);
        }

        if(bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));
        HandleExtrapolation(serverStateBuffer.Get(bufferIndex), CalculateLatencyInMillis(inputPayload));
    }
    void Extrapolate()
    {
        if(IsServer && extrapolationCooldown.IsRunning)
        {
            transform.position += extrapolationState.position.With(y: 0);
        }
    }

    void HandleExtrapolation(StatePayload latest, float latency){
        if(latency < extrapolationLimit && latency > Time.fixedDeltaTime)
        {
            float axisLength = latency * latest.angularVelocity.magnitude * Mathf.Rad2Deg;
            Quaternion angularRotation = Quaternion.AngleAxis(axisLength, latest.angularVelocity);
            if(extrapolationState.position != default){
                latest=extrapolationState;
            }

            var posAdjustment = latest.velocity * (1+latency * extrapolationMultiplier);
            extrapolationState.position = posAdjustment;
            extrapolationState.rotation = angularRotation * latest.rotation;
            extrapolationState.velocity = latest.velocity;
            extrapolationState.angularVelocity = latest.angularVelocity;
            extrapolationCooldown.Start();
        }else{
            extrapolationCooldown.Stop();
            //Reconcile if desired
        }
    }
    [ClientRpc]
    void SendToClientRpc(StatePayload statePayload){
        if(!IsOwner) return;
        lastServerState = statePayload;
    }

    void HandleClientTick(){
        if (!IsClient || !IsOwner) return;
        if(!IsClient) return;
        //if(IsServer) return;

        //Debug.Log("client Tick");
        var currentTick = timer.currentTick;
        var bufferIndex = currentTick % k_bufferSize;
        InputPayload inputPayload = new InputPayload(){
            tick = currentTick,
            timestamp = DateTime.Now,
            networkObjectId =  NetworkObjectId,
            inputVector = input.Move,
            position = transform.position
        };

            if(!IsServer)
            {
                SendToServerRpc(inputPayload);
            }
        
        StatePayload statePayload = ProcessMovement(inputPayload);

        clientStateBuffer.Add(statePayload, bufferIndex);
        HandleServerReconciliation();
    }

    static float CalculateLatencyInMillis(InputPayload inputPayload){
        return(DateTime.Now - inputPayload.timestamp).Milliseconds / 1000f;
    }

    bool ShouldReconcile()
    {
        bool isNewServerState = !lastServerState.Equals(default);
        bool isLastStateUndefinedOrDifferent = lastProcessedState.Equals(default) || !lastProcessedState.Equals(lastServerState);

        return isNewServerState && isLastStateUndefinedOrDifferent && !reconciliationCooldown.IsRunning && !extrapolationCooldown.IsRunning;
    }

    void HandleServerReconciliation(){
        if(!ShouldReconcile())return;

        float positionError;
        int bufferIndex;
        StatePayload rewindState = default;

        bufferIndex = lastServerState.tick % k_bufferSize;
        if(bufferIndex - 1<0) return;

        rewindState= IsHost ? serverStateBuffer.Get( bufferIndex -1) : lastServerState;
        positionError = Vector3.Distance(rewindState.position, clientStateBuffer.Get(bufferIndex).position);

        if(positionError > reconciliationThreshold){
            ReconcileState(rewindState);
            reconciliationCooldown.Start();
        }
        lastProcessedState = lastServerState;
    }

    void ReconcileState(StatePayload rewindState){
        //Debug.Log("reconcile");
        transform.position = rewindState.position;
        transform.rotation = rewindState.rotation;
        rb.velocity = rewindState.velocity;
        rb.angularVelocity = rewindState.angularVelocity;

        if(!rewindState.Equals(lastServerState)) return;

        clientStateBuffer.Add(rewindState, rewindState.tick);

        int tickToReplay = lastServerState.tick;

        while(tickToReplay > timer.currentTick){
            int bufferIndex=tickToReplay % k_bufferSize;
            StatePayload statePayload = ProcessMovement(clientInputBuffer.Get(bufferIndex));
            clientStateBuffer.Add(statePayload, bufferIndex);
            tickToReplay++;
        }
    }


    [ServerRpc]
    void SendToServerRpc(InputPayload input){
        serverInputQueue.Enqueue(input);
    }

    StatePayload ProcessMovement(InputPayload input)
    {
        //Debug.Log("ProcessMovement");
        Move(input.inputVector);
        return new StatePayload(){
            tick = input.tick,
            networkObjectId = input.networkObjectId,
            position = transform.position,
            rotation = transform.rotation,
            velocity = rb.velocity,
            angularVelocity = rb.angularVelocity
        };
    }

    void Move(Vector2 inputVector)
    {
        //Debug.Log("Move");
        float verticalMovement=AdjustInput(input.Move.y);
        float horizontalMovement=AdjustInput(input.Move.x);
        float AdjustInput(float input){
            return input switch{
                >=.7f => 1f,
                <= -.7f => -1f,
                _=> input
            };
        }
        
        moveDirection = orientation.forward * verticalMovement + orientation.right * horizontalMovement;
        slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal);
        float lerpFraction = timer.minTimeBetweenTicks / (1f / Time.deltaTime);

        if(isGrounded)
        {
            rb.useGravity=false;
        }else
        {
            rb.useGravity=true;
        }
        if(isGrounded && !OnSlope())
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * movementMultiplier, ForceMode.Acceleration);
            //Debug.Log("Move");
        }
        else if(isGrounded && OnSlope())
        {
            rb.AddForce(slopeMoveDirection.normalized * moveSpeed * movementMultiplier, ForceMode.Acceleration);
        }
        else if(!isGrounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * movementMultiplier * airMultiplier, ForceMode.Acceleration);
        }
    }
    

    #endregion
}