using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GraphProcessor;

[NodeCustomEditor(typeof(TextureNode))]
public class TextureNodeView : BaseNodeView
{
	VisualElement	shaderCreationUI;
	VisualElement	materialEditorUI;
	MaterialEditor	materialEditor;
	TextureNode		textureNode;

	public override void OnCreated()
	{
		if (textureNode.material != null)
			AssetDatabase.AddObjectToAsset(textureNode.material, owner.graph);
	}

	public override void Enable()
	{
		textureNode = nodeTarget as TextureNode;

		ObjectField shaderField = new ObjectField
		{
			value = textureNode.shader,
			objectType = typeof(Shader),
		};

		shaderField.RegisterValueChangedCallback((v) => {
			owner.RegisterCompleteObjectUndo("Updated Shader of Texture node");
			textureNode.shader = (Shader)v.newValue;
			textureNode.material.shader = textureNode.shader;
			UpdateShaderCreationUI();
		});

		controlsContainer.Add(shaderField);

		shaderCreationUI = new VisualElement();
		controlsContainer.Add(shaderCreationUI);
		UpdateShaderCreationUI();

		materialEditorUI = new VisualElement();
		materialEditorUI.Add(new IMGUIContainer(MaterialGUI));
		controlsContainer.Add(materialEditorUI);

		materialEditor = Editor.CreateEditor(textureNode.material) as MaterialEditor;
	}

	void UpdateShaderCreationUI()
	{
		shaderCreationUI.Clear();

		if (textureNode.shader?.name == TextureNode.DefaultShaderName)
		{
			shaderCreationUI.Add(new Button(CreateEmbeededShader) {
				text = "New Shader"
			});
		}
		else
		{
			shaderCreationUI.Add(new Button(OpenCurrentShader){
				text = "Open"
			});
		}

		void CreateEmbeededShader()
		{
			Debug.Log("TODO");
		}

		void OpenCurrentShader()
		{
			AssetDatabase.OpenAsset(textureNode.shader);
		}
	}

	void MaterialGUI()
	{
		materialEditor.PropertiesGUI();
	}

	public override void OnRemoved()
	{
		AssetDatabase.RemoveObjectFromAsset(textureNode.material);
	}
}