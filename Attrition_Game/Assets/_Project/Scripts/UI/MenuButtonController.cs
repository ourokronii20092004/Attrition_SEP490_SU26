using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuButtonController : MonoBehaviour {

	public int index;
	[SerializeField] bool keyDown;
	[SerializeField] int maxIndex;
	public AudioSource audioSource;

	[Tooltip("Tự động đếm số MenuButton trong scene để set maxIndex")]
	[SerializeField] bool autoDetectMaxIndex = true;

	void Start () {
		audioSource = GetComponent<AudioSource>();

		if (autoDetectMaxIndex)
		{
			MenuButton[] buttons = FindObjectsByType<MenuButton>(FindObjectsSortMode.None);
			if (buttons.Length > 0)
			{
				maxIndex = buttons.Length - 1;
			}
		}
	}
	
	void Update () {
		if(Input.GetAxis ("Vertical") != 0){
			if(!keyDown){
				if (Input.GetAxis ("Vertical") < 0) {
					if(index < maxIndex){
						index++;
					}else{
						index = 0;
					}
				} else if(Input.GetAxis ("Vertical") > 0){
					if(index > 0){
						index --; 
					}else{
						index = maxIndex;
					}
				}
				keyDown = true;
			}
		}else{
			keyDown = false;
		}
	}

}
