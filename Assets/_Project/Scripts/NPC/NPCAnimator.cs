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
        private static readonly int WalkMultHash = Animator.StringToHash("WalkMult");

        [Header("References")]
        [SerializeField] private NPCVisitor visitor;
        [SerializeField] private NavMeshAgent agent;

        [Header("Tuning")]
        [Tooltip("How quickly the Animator Speed parameter follows NavMeshAgent velocity.")]
        [SerializeField] private float speedSmoothing = 12f;
        [Tooltip("Ground speed (m/s) at which the Walk clip looks planted at 1x playback. " +
                 "The clip is sped up/slowed so the feet match the agent's actual speed (kills foot sliding).")]
        [SerializeField] private float referenceWalkSpeed = 1.3f;
        [Tooltip("Upper bound for the walk playback multiplier so fast agents never look absurd.")]
        [SerializeField] private float maxWalkMultiplier = 3f;

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

            // Scale Walk playback to the real ground speed so the feet stay planted (no sliding).
            // Floored at 1 so Idle keeps its natural speed when the agent is standing still.
            float walkMult = referenceWalkSpeed > 0.01f
                ? Mathf.Clamp(_smoothedSpeed / referenceWalkSpeed, 1f, maxWalkMultiplier)
                : 1f;

            _animator.SetFloat(SpeedHash, _smoothedSpeed);
            _animator.SetBool(TalkingHash, isTalking);
            _animator.SetFloat(WalkMultHash, walkMult);
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
