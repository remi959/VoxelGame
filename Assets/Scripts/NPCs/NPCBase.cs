using System.Collections.Generic;
using Assets.Scripts.NPCs.States;
using UnityEngine;

namespace Assets.Scripts.NPCs
{
    [RequireComponent(typeof(NPCMotor))]
    public abstract class NPCBase : MonoBehaviour
    {
        [Header("NPC Settings")]
        [SerializeField] protected string npcName = "Unit";
        [SerializeField] protected float maxHealth = 100f;

        [Header("Selection Visuals")]
        [SerializeField] protected GameObject selectionIndicator;

        private static readonly HashSet<NPCBase> allNPCs = new();
        public static IReadOnlyCollection<NPCBase> All => allNPCs;

        protected float currentHealth;
        protected NPCMotor motor;
        protected bool isSelected = false;
        protected StateMachine stateMachine;

        public NPCMotor Motor => motor;

        protected virtual void OnEnable() => allNPCs.Add(this);
        protected virtual void OnDisable() => allNPCs.Remove(this);

        protected virtual void Awake()
        {
            motor = GetComponent<NPCMotor>();
            currentHealth = maxHealth;

            InitializeStateMachine();
        }

        protected virtual void InitializeStateMachine()
        {
            stateMachine = new StateMachine();
            stateMachine.AddState(new WorkerIdleState(this));
            stateMachine.SetState<WorkerIdleState>();
        }

        protected virtual void Start()
        {
            if (selectionIndicator != null) selectionIndicator.SetActive(false);
        }

        protected virtual void Update()
        {
            stateMachine?.Update();
        }

        public virtual void OnSelected()
        {
            isSelected = true;
            if (selectionIndicator != null) selectionIndicator.SetActive(true);

            Debug.Log($"{npcName} selected");
        }

        public virtual void OnDeselected()
        {
            isSelected = false;
            if (selectionIndicator != null) selectionIndicator.SetActive(false);

            Debug.Log($"{npcName} deselected");
        }

        public virtual void MoveTo(Vector3 destination) => motor.SetDestination(destination);

        public virtual void Stop() => motor.Stop();

        public virtual void TakeDamage(float damage)
        {
            currentHealth -= damage;
            if (currentHealth <= 0) Die();
        }

        protected virtual void Die()
        {
            Debug.Log($"{npcName} died");
            Destroy(gameObject);
        }

        /// <summary>
        /// Called when right-clicking on an interactable object.
        /// Override in subclasses to handle specific interactions.
        /// </summary>
        public virtual void InteractWith(GameObject target)
        {
            // Default: just move to it
            MoveTo(target.transform.position);
        }
    }
}