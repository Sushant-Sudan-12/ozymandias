using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace KKK.UI
{
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("Name of the speaker (e.g. 'Narrator', 'Cultist', 'Guide').")]
        public string speakerName = "Guide";

        [Tooltip("The dialogue text to display.")]
        [TextArea(2, 5)]
        public string text = "Welcome to the forest. Stay alert.";

        [Tooltip("Optional character portrait sprite.")]
        public Sprite portrait;

        [Tooltip("Delay between each character typed. Set to 0 for instant text.")]
        [Range(0f, 0.08f)]
        public float typingSpeed = 0.025f;

        [Tooltip("Optional voice/audio clip played at the start of this line.")]
        public AudioClip voiceClip;
    }

    [System.Serializable]
    public class DialogueSequence
    {
        [Tooltip("Identifier for this dialogue sequence (e.g. 'Intro', 'Victory', 'Statue').")]
        public string dialogueId = "Dialogue";

        [Tooltip("List of dialogue lines played in order.")]
        public List<DialogueLine> lines = new List<DialogueLine>();

        [Tooltip("Event fired when the entire dialogue sequence finishes.")]
        public UnityEvent onComplete;

        public DialogueSequence()
        {
            lines = new List<DialogueLine>();
        }

        public DialogueSequence(string id, params string[] texts)
        {
            dialogueId = id;
            lines = new List<DialogueLine>();
            foreach (var t in texts)
            {
                lines.Add(new DialogueLine { speakerName = "Guide", text = t });
            }
        }
    }
}
