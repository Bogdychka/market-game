using UnityEngine;
using UnityEngine.AI;

namespace Market.NPC
{
    /// <summary>
    /// Drives NPC humanoid animation parameters from visitor state and NavMeshAgent velocity.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NPCAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int TalkingHash = Animator.StringToHash("Talking");

        [Header("References")]
        [SerializeField] private NPCVisitor visitor;
        [SerializeField] private NavMeshAgent agent;

        [Header("Tuning")]
        [Tooltip("How quickly the Animator Speed parameter follows NavMeshAgent velocity.")]
        [SerializeField] private float speedSmoothing = 12f;

        private Animator _animator;
        private float _smoothedSpeed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            ResolveReferences();
            ValidateReferences();
        }

        private void Update()
        {
            if (_animator == null) return;

            float targetSpeed = agent != null ? agent.velocity.magnitude : 0f;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, targetSpeed, Time.deltaTime * speedSmoothing);

            bool isTalking = visitor != null && visitor.CurrentState == NPCVisitor.State.Browsing;
            _animator.SetFloat(SpeedHash, _smoothedSpeed);
            _animator.SetBool(TalkingHash, isTalking);
        }

        private void ResolveReferences()
        {
            if (visitor == null)
                visitor = GetComponentInParent<NPCVisitor>();

            if (agent == null)
                agent = GetComponentInParent<NavMeshAgent>();
        }

        private void ValidateReferences()
        {
            if (visitor == null) Debug.LogError("[NPCAnimator] visitor not assigned.", this);
            if (agent == null) Debug.LogError("[NPCAnimator] agent not assigned.", this);
        }
    }
}
