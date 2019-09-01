using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using GraphProcessor;
using System.Linq;

namespace Mixture
{
	[NodeCustomEditor(typeof(FixedShaderNode))]
	public class MixtureNodeView : BaseNodeView
	{
		protected VisualElement propertyEditorUI;
        protected VisualElement previewContainer;

        protected new MixtureGraphView  owner => base.owner as MixtureGraphView;
		protected new MixtureNode       nodeTarget => base.nodeTarget as MixtureNode;

		Dictionary< Material, MaterialProperty[] >  oldMaterialProperties = new Dictionary<Material, MaterialProperty[]>();
		Dictionary< Material, MaterialEditor >      materialEditors = new Dictionary<Material, MaterialEditor>();

		protected virtual string header => string.Empty;

		protected virtual bool hasPreview => false;
		protected override bool hasSettings => nodeTarget.hasSettings;

		protected override VisualElement CreateSettingsView()
		{
			var view = new MixtureRTSettingsView(nodeTarget, owner);
            view.AddToClassList("RTSettingsView");
			view.RegisterChangedCallback(nodeTarget.OnSettingsChanged);

			return view;
		}

		const string stylesheetName = "MixtureCommon";

        public override void Enable()
		{
            var mixtureNode = nodeTarget as MixtureNode;
			var stylesheet = Resources.Load<StyleSheet>(stylesheetName);
			if(!styleSheets.Contains(stylesheet))
				styleSheets.Add(stylesheet);

			// When we change the output dimension, we want to update the output ports
			// TODO: there is probably a race condition here between the port that changes type
			// and the MixtureGraphView callback that run the processor
			owner.graph.onOutputTextureUpdated += UpdatePorts;
			nodeTarget.onSettingsChanged += UpdatePorts;
			nodeTarget.onSettingsChanged += () => owner.processor.Run();
			
			propertyEditorUI = new VisualElement();
			controlsContainer.Add(propertyEditorUI);

			// Fix the size of the node
			style.width = nodeTarget.nodeWidth;

			propertyEditorUI.AddToClassList("PropertyEditorUI");
			controlsContainer.AddToClassList("ControlsContainer");

			if (header != string.Empty)
			{
				var title = new Label(header);
				title.AddToClassList("PropertyEditorTitle");
				propertyEditorUI.Add(title);
			}

			if (hasPreview)
			{
                CreateTexturePreview(ref previewContainer, mixtureNode.previewTexture); // TODO : Add Slice Preview
                controlsContainer.Add(previewContainer);
            }

            propertyEditorUI.style.display = DisplayStyle.Flex;
        }

		~MixtureNodeView()
		{
			MixturePropertyDrawer.UnregisterGraph(owner.graph);
		}

		void UpdatePorts()
		{
			nodeTarget.UpdateAllPorts();
			RefreshPorts();
		}

		bool CheckPropertyChanged(Material material, MaterialProperty[] properties)
		{
			bool propertyChanged = false;
			MaterialProperty[]  oldProperties;
			oldMaterialProperties.TryGetValue(material, out oldProperties);

			if (oldProperties != null)
			{
				// Check if shader was changed (new/deleted properties)
				if (properties.Length != oldProperties.Length)
				{
					propertyChanged = true;
				}
				else
				{
					for (int i = 0; i < properties.Length; i++)
					{
						if (properties[i].type != oldProperties[i].type)
							propertyChanged = true;
						if (properties[i].displayName != oldProperties[i].displayName)
							propertyChanged = true;
						if (properties[i].flags != oldProperties[i].flags)
							propertyChanged = true;
						if (properties[i].name != oldProperties[i].name)
							propertyChanged = true;
					}
				}
			}

			oldMaterialProperties[material] = MaterialEditor.GetMaterialProperties(new []{material});

			return propertyChanged;
		}

		// Custom property draw, we don't want things that are connected to an edge or useless like the render queue
		protected bool MaterialPropertiesGUI(Material material, bool autoLabelWidth = true)
		{
			if (material == null || material.shader == null)
				return false;
				
			if (autoLabelWidth)
			{
				EditorGUIUtility.wideMode = false;
				EditorGUIUtility.labelWidth = nodeTarget.nodeWidth / 3.0f;
			}

			MaterialProperty[] properties = MaterialEditor.GetMaterialProperties(new []{material});
			var portViews = GetPortViewsFromFieldName(nameof(ShaderNode.materialInputs));

			MaterialEditor  editor;
			if (!materialEditors.TryGetValue(material, out editor))
			{
				editor = materialEditors[material] = Editor.CreateEditor(material) as MaterialEditor;
				MixturePropertyDrawer.RegisterEditor(editor, this, owner.graph);
			}

			bool propertiesChanged = CheckPropertyChanged(material, properties);

			foreach (var property in properties)
			{
				if ((property.flags & (MaterialProperty.PropFlags.HideInInspector | MaterialProperty.PropFlags.PerRendererData)) != 0)
					continue;

				// Retrieve the port view from the property name
				var portView = portViews?.FirstOrDefault(p => p.portData.identifier == property.name);
				if (portView != null && portView.connected)
					continue;

				float h = editor.GetPropertyHeight(property, property.displayName);
				Rect r = EditorGUILayout.GetControlRect(true, h, EditorStyles.layerMaskField);

				editor.ShaderProperty(r, property, property.displayName);
			}

			return propertiesChanged;
		}

		protected void CreateTexturePreview(ref VisualElement previewContainer, Texture texture, int currentSlice = 0)
		{
			if(previewContainer == null)
                previewContainer = new VisualElement();
			else
            	previewContainer.Clear();



			if (texture == null)
				return;

            switch (texture.dimension)
			{
				case TextureDimension.Tex2D:
					CreateTexture2DPreview(previewContainer, texture);
					break;
				case TextureDimension.Tex2DArray:
					CreateTexture2DArrayPreview(previewContainer, texture, currentSlice);
					break;
				case TextureDimension.Tex3D:
					CreateTexture3DPreview(previewContainer, texture, currentSlice);
					break;
				case TextureDimension.Cube:
					CreateTextureCubePreview(previewContainer, texture, currentSlice);
					break;
				default:
					Debug.LogError(texture + " is not a supported type for preview");
					return;
			}
        }

		Rect GetPreviewRect(Texture texture)
		{
			float width = Mathf.Min(nodeTarget.nodeWidth, texture.width);
			float height = Mathf.Min(nodeTarget.nodeWidth, texture.height);
			return GUILayoutUtility.GetRect(1, width, 1, height);
		}

        enum PreviewMode
        {
            RGBA,
            RGB,
            Alpha
        }
        PreviewMode m_PreviewMode = PreviewMode.RGBA;

		void CreateTexture2DPreview(VisualElement previewContainer, Texture texture)
		{
			var previewImageSlice = new IMGUIContainer(() => {
                // square image:
                using(new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(16)))
                {
                    if (GUILayout.Button(m_PreviewMode.ToString(), EditorStyles.toolbarButton))
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("RGBA"), m_PreviewMode == PreviewMode.RGBA, () => m_PreviewMode = PreviewMode.RGBA);
                        menu.AddItem(new GUIContent("RGB"), m_PreviewMode == PreviewMode.RGB, () => m_PreviewMode = PreviewMode.RGB);
                        menu.AddItem(new GUIContent("Alpha"), m_PreviewMode == PreviewMode.Alpha, () => m_PreviewMode = PreviewMode.Alpha);
                        Rect r = GUILayoutUtility.GetLastRect();
                        r.xMin += 8;
                        r.yMax += 16;
                        menu.DropDown(r);
                    }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(8);
                switch(m_PreviewMode)
                {
                    case PreviewMode.RGBA:
                        EditorGUI.DrawTextureTransparent(GetPreviewRect(texture), texture, ScaleMode.ScaleToFit, 0, 0);
                        break;
                    case PreviewMode.RGB:
                        EditorGUI.DrawPreviewTexture(GetPreviewRect(texture), texture, null, ScaleMode.ScaleToFit, 0, 0);
                        break;
                    case PreviewMode.Alpha:
                        EditorGUI.DrawTextureAlpha(GetPreviewRect(texture), texture, ScaleMode.ScaleToFit, 0, 0);
                        break;
                }
                GUILayout.Space(8);

            });
			previewContainer.Add(previewImageSlice);
		}

		void CreateTexture2DArrayPreview(VisualElement previewContainer, Texture texture, int currentSlice)
		{
			var previewSliceIndex = new SliderInt(0, TextureUtils.GetSliceCount(texture) - 1)
			{
				label = "Slice",
				value = currentSlice,
			};
			var previewImageSlice = new IMGUIContainer(() => {
				// square image:
				MixtureUtils.textureArrayPreviewMaterial.SetTexture("_TextureArray", texture);
				MixtureUtils.textureArrayPreviewMaterial.SetFloat("_Slice", currentSlice);
				EditorGUI.DrawPreviewTexture(GetPreviewRect(texture), Texture2D.whiteTexture, MixtureUtils.textureArrayPreviewMaterial, ScaleMode.ScaleToFit, 0, 0);
            });
			previewSliceIndex.RegisterValueChangedCallback((ChangeEvent< int > a) => {
				currentSlice = a.newValue;
			});
			previewContainer.Add(previewSliceIndex);
			previewContainer.Add(previewImageSlice);
		}
		
		void CreateTexture3DPreview(VisualElement previewContainer, Texture texture, int currentSlice)
		{
			// TODO: 3D Texture preview material with ray-marching
			var previewSliceIndex = new SliderInt(0, TextureUtils.GetSliceCount(texture) - 1)
			{
				label = "Slice",
				value = currentSlice,
			};
			var previewImageSlice = new IMGUIContainer(() => {
				// square image:
				MixtureUtils.texture3DPreviewMaterial.SetTexture("_Texture3D", texture);
				MixtureUtils.texture3DPreviewMaterial.SetFloat("_Depth", ((float)currentSlice + 0.5f) / nodeTarget.rtSettings.GetDepth(owner.graph));
				EditorGUI.DrawPreviewTexture(GetPreviewRect(texture), Texture2D.whiteTexture, MixtureUtils.texture3DPreviewMaterial, ScaleMode.ScaleToFit, 0, 0);
            });
			previewSliceIndex.RegisterValueChangedCallback((ChangeEvent< int > a) => {
				currentSlice = a.newValue;
			});
			previewContainer.Add(previewSliceIndex);
			previewContainer.Add(previewImageSlice);
		}

		void CreateTextureCubePreview(VisualElement previewContainer, Texture texture, int currentSlice)
		{
			var previewImageSlice = new IMGUIContainer(() => {
				// square image:
				MixtureUtils.textureCubePreviewMaterial.SetTexture("_Cubemap", texture);
				MixtureUtils.textureCubePreviewMaterial.SetFloat("_Slice", currentSlice);
				EditorGUI.DrawPreviewTexture(GetPreviewRect(texture), Texture2D.whiteTexture, MixtureUtils.textureCubePreviewMaterial, ScaleMode.ScaleToFit, 0, 0);
            });
			previewContainer.Add(previewImageSlice);
		}
	}
}