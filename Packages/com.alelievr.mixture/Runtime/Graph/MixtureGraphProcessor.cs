// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;
// using GraphProcessor;

// namespace Mixture
// {
// 	public class MixtureGraphProcessor : BaseGraphProcessor
// 	{
// 		List< BaseNode >		processList;
// 		new MixtureGraph		graph => base.graph as MixtureGraph;

// 		Dictionary< CustomRenderTexture, List< BaseNode > > customTextureDependencies = new Dictionary<CustomRenderTexture, List<BaseNode>>();

// 		public MixtureGraphProcessor(BaseGraph graph) : base(graph)
// 		{
// 			CustomRenderTextureManager.beforeProcessing -= OnBeforeCRTProcessed;
// 			CustomRenderTextureManager.beforeProcessing += OnBeforeCRTProcessed;
// 		}

// 		~MixtureGraphProcessor()
// 		{
// 			CustomRenderTextureManager.beforeProcessing -= OnBeforeCRTProcessed;
// 		}

// 		void OnBeforeCRTProcessed(CustomRenderTexture crt)
// 		{
// 			Debug.Log(crt);
// 			// Update all static parameters of the CRT
// 		}

// 		public override void UpdateComputeOrder() {}

// 		public override void Run()
// 		{
// 			// Realtime graphs don't need to be processed by hand, everything is handeled by custom texture callbacks
// 			if (graph.isRealtime)
// 				return;

// 			processList = graph.nodes.OrderBy(n => n.computeOrder).ToList();

// 			int count = processList.Count;

// 			// The process of the mixture graph will update all CRTs,
// 			// assign their materials and set local material values
// 			for (int i = 0; i < count; i++)
// 			{
// 				var node = processList[i];

// 				// node.OnProcess(); // AHHHHHh

// 				// Temporary hack: Custom Textures are not updated when the Shader / the Material is updated
// 				// and inside the dependency tree of a CRT. So we need to manually update all CRTs.
// 				if (node is ShaderNode s)
// 				{
// 					// the CRT output will be null if there are processing errors
// 					if (s.output != null)
// 					{
// 						customTextureDependencies[s.output] = GetMixtureNodeDependencies(s).ToList();
// 						s.output.Update();
// 					}
// 				}
// 			}

// 			if (graph.outputNode.tempRenderTexture != null)
// 				graph.outputNode.tempRenderTexture.Update();

//             var external = graph.nodes.FindAll(node => node.GetType() == typeof(ExternalOutputNode));
//             foreach(var node in external)
//             {
//                 (node as ExternalOutputNode).tempRenderTexture.Update();
//             }
// 		}

// 		IEnumerable< BaseNode > GetMixtureNodeDependencies(BaseNode node)
// 		{
// 			foreach (var n in node.GetInputNodes())
// 			{
// 				if (n is INeedsCPU)
// 					foreach (var d in GetMixtureNodeDependencies(n))
// 						yield return d;
// 				else
// 					yield break;
// 			}
// 		}
// 	}
// }

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Jobs;
using Unity.Collections;
using GraphProcessor;

namespace Mixture
{
	public class MixtureGraphProcessor : BaseGraphProcessor
	{
		List< BaseNode >		processList;
		new MixtureGraph		graph => base.graph as MixtureGraph;

		Dictionary<CustomRenderTexture, List<BaseNode>> mixtureDependencies = new Dictionary<CustomRenderTexture, List<BaseNode>>();

		public MixtureGraphProcessor(BaseGraph graph) : base(graph)
		{
			CustomTextureManager.onBeforeCustomTextureUpdated -= BeforeCustomRenderTextureUpdate;
			CustomTextureManager.onBeforeCustomTextureUpdated += BeforeCustomRenderTextureUpdate;

			foreach (var node in graph.nodes)
				node.OnProcess();
		}

		public override void UpdateComputeOrder() {}

		void BeforeCustomRenderTextureUpdate(CustomRenderTexture crt)
		{
			if (mixtureDependencies.TryGetValue(crt, out var dependencies))
			{
				// Update the dependencies of the CRT
				foreach (var nonCRTDep in dependencies)
					nonCRTDep.OnProcess();
			}
		}

		public override void Run()
		{
			mixtureDependencies.Clear();

			processList = graph.nodes.OrderBy(n => n.computeOrder).ToList();

			// For now we process every node multiple time,
			// future improvement: only refresh nodes when  asked by the CRT
			foreach (BaseNode node in processList)
			{
				node.OnProcess();

				if (node is IUseCustomRenderTextureProcessing iUseCRT)
				{
					var mixtureNode = node as MixtureNode;
					var crt = iUseCRT.GetCustomRenderTexture();
					if (crt != null)
					{
						mixtureDependencies.Add(crt, mixtureNode.GetMixtureDependencies());
						crt.Update();
					}
				}
			}

			CustomTextureManager.ForceUpdateNow();
		}
	}
}