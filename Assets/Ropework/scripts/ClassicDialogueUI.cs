using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

namespace Yarn.Unity.Example {
    public class ClassicDialogueUI : Yarn.Unity.DialogueUIBehaviour {
        public Ropework.RopeworkManager ropework;
        public Text nameText;
        public Text lineText;
        public Button NextButton;
        public Button PrevButton;

        public GameObject dialogueContainer;
        public GameObject continuePrompt;

        private Yarn.OptionChooser SetSelectedOption;

        [Tooltip("How quickly to show the text, in seconds per character")]
        public float textSpeed = 0.025f;

        public List<Button> optionButtons;
        public RectTransform gameControlsContainer;

        // Class to store the full state of each dialogue "scene"
        private class DialogueState {
            public string Speaker;
            public string Text;
            public List<string> Options;
            public bool IsChoice;

            public DialogueState(string speaker, string text, List<string> options, bool isChoice) {
                Speaker = speaker;
                Text = text;
                Options = options;
                IsChoice = isChoice;
            }
        }

        // Stack to store the full dialogue state history
        private Stack<DialogueState> dialogueHistory = new Stack<DialogueState>();

        // A flag to track if the user is viewing history
        private bool viewingHistory = false;

        // Track the current line in the script
        private int currentLineIndex = 0;

        // Store the list of dialogue lines to resume progression
        private List<Yarn.Line> dialogueLines = new List<Yarn.Line>();

        void Awake() {
            if (ropework == null) {
                ropework = FindObjectOfType<Ropework.RopeworkManager>();
            }
            if (NextButton == null || PrevButton == null || lineText == null || nameText == null) {
                Debug.LogError("UI elements are not assigned!");
                return;
            }

            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);

            lineText.gameObject.SetActive(false);
            NextButton.gameObject.SetActive(false);
            PrevButton.gameObject.SetActive(false);

            foreach (var button in optionButtons) {
                button.gameObject.SetActive(false);
            }

            if (continuePrompt != null)
                continuePrompt.SetActive(false);
        }

        public override IEnumerator RunLine(Yarn.Line line) {
            if (lineText == null || nameText == null || NextButton == null || PrevButton == null) {
                Debug.LogError("UI elements are not assigned in the inspector!");
                yield break;
            }

            // If viewing history, ignore new lines until back to the present
            if (viewingHistory) {
                yield break;
            }

            // Add the line to the dialogue progression list if it hasn't been added already
            if (currentLineIndex >= dialogueLines.Count) {
                dialogueLines.Add(line);
            }

            // If there is text already displayed, save the current state to the history
            if (!string.IsNullOrEmpty(lineText.text)) {
                var currentOptions = new List<string>();
                foreach (var button in optionButtons) {
                    if (button.gameObject.activeSelf) {
                        currentOptions.Add(button.GetComponentInChildren<Text>().text);
                    }
                }

                var currentState = new DialogueState(nameText.text, lineText.text, currentOptions, currentOptions.Count > 0);
                dialogueHistory.Push(currentState);

                PrevButton.gameObject.SetActive(true); // Enable PrevButton if there's history
            }

            // Show the new line
            string speakerName = "";
            string lineTextDisplay = line.text;
            if (line.text.Contains(":")) {
                var splitLine = line.text.Split(new char[] { ':' }, 2);
                speakerName = splitLine[0].Trim();
                lineTextDisplay = splitLine[1].Trim();
            }

            UpdateDialogueUI(speakerName, lineTextDisplay);

            if (textSpeed > 0.0f) {
                var stringBuilder = new StringBuilder();
                bool earlyOut = false;
                foreach (char c in lineTextDisplay) {
                    stringBuilder.Append(c);
                    lineText.text = stringBuilder.ToString();
                    yield return new WaitForSeconds(textSpeed);
                    if (NextButton.gameObject.activeSelf && NextButton.onClick != null) {
                        earlyOut = true;
                        break;
                    }
                }

                if (earlyOut) {
                    lineText.text = lineTextDisplay;
                }
            } else {
                lineText.text = lineTextDisplay;
            }

            NextButton.gameObject.SetActive(true);

            // Clear previous listeners and add a new one
            NextButton.onClick.RemoveAllListeners();
            bool buttonClicked = false;
            NextButton.onClick.AddListener(() => buttonClicked = true);

            while (!buttonClicked) {
                yield return null;
            }

            NextButton.gameObject.SetActive(false);

            // Increment the current line index
            currentLineIndex++;
        }

        // New method to handle going back to a previous dialogue state
        public void OnPrevButtonClicked() {
            if (dialogueHistory.Count > 0) {
                viewingHistory = true;  // Indicate we're now in history mode
                var previousState = dialogueHistory.Pop();
                RestoreDialogueState(previousState);

                // Decrement current line index to allow resuming forward progression correctly
                currentLineIndex = Mathf.Max(0, currentLineIndex - 1);
            }

            if (dialogueHistory.Count == 0) {
                PrevButton.gameObject.SetActive(false);
            }
        }

        // Restore the full state of the dialogue
        private void RestoreDialogueState(DialogueState state) {
            UpdateDialogueUI(state.Speaker, state.Text);

            if (state.IsChoice && state.Options != null && state.Options.Count > 0) {
                for (int i = 0; i < state.Options.Count && i < optionButtons.Count; i++) {
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].GetComponentInChildren<Text>().text = state.Options[i];
                }
            } else {
                foreach (var button in optionButtons) {
                    button.gameObject.SetActive(false);
                }
            }
        }

        // Update the UI with new dialogue information
        private void UpdateDialogueUI(string speaker, string text) {
            if (!string.IsNullOrEmpty(speaker)) {
                nameText.transform.parent.gameObject.SetActive(true);
                nameText.text = speaker;
                if (ropework.actorColors.ContainsKey(speaker)) {
                    nameText.transform.parent.GetComponent<Image>().color = ropework.actorColors[speaker];
                }
                if (ropework.actors.ContainsKey(speaker)) {
                    ropework.HighlightSprite(ropework.actors[speaker]);
                }
            } else {
                nameText.transform.parent.gameObject.SetActive(false);
            }

            lineText.text = text;
            lineText.gameObject.SetActive(true);
        }

        public void OnNextButtonClicked() {
            // Exit history mode when Next is clicked
            if (viewingHistory) {
                viewingHistory = false;

                // Run the next line from where we left off
                RunNextLine();
            }
        }

        // This method should be hooked into the NextButton's click event
        private void RunNextLine() {
            // Resume dialogue from where it left off if not at the end
            if (currentLineIndex < dialogueLines.Count) {
                StartCoroutine(RunLine(dialogueLines[currentLineIndex]));
            }
        }

        public override IEnumerator RunOptions(Yarn.Options optionsCollection, Yarn.OptionChooser optionChooser) {
            if (optionsCollection.options.Count > optionButtons.Count) {
                Debug.LogWarning("There are more options to present than there are buttons to present them in. This will cause problems.");
            }

            int i = 0;
            foreach (var optionString in optionsCollection.options) {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].GetComponentInChildren<Text>().text = optionString;
                i++;
            }

            SetSelectedOption = optionChooser;

            while (SetSelectedOption != null) {
                yield return null;
            }

            foreach (var button in optionButtons) {
                button.gameObject.SetActive(false);
            }
        }

        public void SetOption(int selectedOption) {
            if (SetSelectedOption == null) {
                return;
            }

            SetSelectedOption(selectedOption);
            SetSelectedOption = null;
        }

        public override IEnumerator RunCommand(Yarn.Command command) {
            Debug.Log("Command: " + command.text);
            yield break;
        }

        public override IEnumerator DialogueStarted() {
            Debug.Log("Dialogue starting!");

            if (dialogueContainer != null)
                dialogueContainer.SetActive(true);

            if (gameControlsContainer != null) {
                gameControlsContainer.gameObject.SetActive(false);
            }

            yield break;
        }

        public override IEnumerator DialogueComplete() {
            Debug.Log("Complete!");

            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);

            if (gameControlsContainer != null) {
                gameControlsContainer.gameObject.SetActive(true);
            }

            // Clear dialogue history when dialogue ends
            dialogueHistory.Clear();
            dialogueLines.Clear(); // Clear dialogue lines

            yield break;
        }
    }
}
