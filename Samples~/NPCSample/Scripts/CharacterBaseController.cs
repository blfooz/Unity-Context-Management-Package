using System.Collections.Generic;
using System.Threading.Tasks;
using ContextManagement;
using TMPro;
using UnityEngine;

public abstract class CharacterBaseController : MonoBehaviour
{
    public float bubbleFadeSpeed = 0.3f;
    protected TMP_Text speechBubble;
    private CircleCollider2D speakRange;

    protected virtual void Start()
    {
        speechBubble = GetComponentInChildren<TMP_Text>();
        if (speechBubble == null) Debug.LogError("Speech Bubble not found.");
        speechBubble.alpha = 0;

        speakRange = GetComponent<CircleCollider2D>();
        if (speakRange == null) Debug.LogWarning("No speak range.");
    }

    protected HashSet<GameObject> entitiesInRange = new();


    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        entitiesInRange.Add(collision.gameObject);

    }

    protected virtual void OnTriggerExit2D(Collider2D collision)
    {
        entitiesInRange.Remove(collision.gameObject);
    }

    protected void Speak(string message)
    {
        speechBubble.text = message;
        speechBubble.alpha = 1;
        foreach (var entity in entitiesInRange)
        {
            if (entity.TryGetComponent<NPCController>(out var npc))
                npc.Listen(message);
        }
    }

    public float goToSpeed = 3f;
    int goToRequestId;

    public async Task GoTo(Vector2 position)
    {
        int requestId = ++goToRequestId;

        while (requestId == goToRequestId &&
               Vector2.Distance(transform.position, position) > 0.05f)
        {
            transform.position = Vector2.MoveTowards(transform.position, position, goToSpeed * Time.deltaTime);
            await Awaitable.NextFrameAsync();
        }

        if (requestId == goToRequestId)
            transform.position = position;
    }

    public async Task GoTo(Transform targetTransform, float proximity = 0.5f)
    {
        int requestId = ++goToRequestId;

        while (requestId == goToRequestId &&
               Vector2.Distance(transform.position, targetTransform.position) > proximity)
        {
            transform.position = Vector2.MoveTowards(transform.position, targetTransform.position, goToSpeed * Time.deltaTime);
            await Awaitable.NextFrameAsync();
        }
    }

    bool _doFade = true;
    protected virtual void Update()
    {
        if (_doFade && speechBubble.alpha > 0)
        {
            speechBubble.alpha -= bubbleFadeSpeed * Time.deltaTime;
        }

    }
}
