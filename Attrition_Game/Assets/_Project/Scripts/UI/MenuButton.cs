using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
	[SerializeField] MenuButtonController menuButtonController;
	[SerializeField] Animator animator;
	[SerializeField] AnimatorFunctions animatorFunctions;
	[SerializeField] int thisIndex;

	private bool mousePressed = false;

    void Update()
    {
		if(menuButtonController.index == thisIndex)
		{
			animator.SetBool ("selected", true);

			// Don't interfere with mouse press handling
			if(!mousePressed)
			{
				if(Input.GetAxis ("Submit") == 1){
					animator.SetBool ("pressed", true);
				}else if (animator.GetBool ("pressed")){
					animator.SetBool ("pressed", false);
					animatorFunctions.disableOnce = true;
				}
			}
		}else{
			animator.SetBool ("selected", false);
		}
    }

	public void OnPointerEnter(PointerEventData eventData)
	{
		menuButtonController.index = thisIndex;
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		menuButtonController.index = thisIndex;
		animator.SetBool ("pressed", true);
		mousePressed = true;
		StartCoroutine(ResetMousePress());
	}

	private IEnumerator ResetMousePress()
	{
		yield return new WaitForSeconds(0.2f);
		animator.SetBool ("pressed", false);
		animatorFunctions.disableOnce = true;
		mousePressed = false;
	}
}
