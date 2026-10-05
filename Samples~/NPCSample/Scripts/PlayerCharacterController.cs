using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerCharacterController : CharacterBaseController
{
	public float moveSpeed = 1;
	public InputAction moveAction, startSpeakingAction, sendSpeakingAction;

	public TMP_InputField input;

	protected override void Start()
	{
		base.Start();
		startSpeakingAction.performed += (_) => StartInput();
		sendSpeakingAction.performed += SendInput;
		input.onSelect.AddListener((_) => _speaking = true);
	}

	protected override void Update()
	{
		base.Update();
		if (!_speaking)
			transform.Translate(moveSpeed * Time.deltaTime * (Vector3)moveAction.ReadValue<Vector2>());
	}

	bool _speaking;
	void StartInput()
	{
		input.Select();
		//_speaking = true;
	}

	void SendInput(InputAction.CallbackContext context)
	{
		Speak(input.text);
		input.text = "";
		EventSystem.current.SetSelectedGameObject(null);
		_speaking = false;
	}

	void OnEnable()
	{
		moveAction.Enable();
		startSpeakingAction.Enable();
		sendSpeakingAction.Enable();
	}

	void OnDisable()
	{
		moveAction.Disable();
		startSpeakingAction.Disable();
		sendSpeakingAction.Disable();
	}
}