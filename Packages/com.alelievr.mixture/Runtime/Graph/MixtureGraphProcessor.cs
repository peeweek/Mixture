using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using GraphProcessor;
using UnityEngine.Rendering;

namespace Mixture
{
	public interface INeedLoopReset
	{
		void PrepareNewIteration();
	}

	public class ComputeOrderInfo
	{
		public Dictionary<BaseNode, int> foreachLoopLevel = new Dictionary<BaseNode, int>();

		public void Clear() => foreachLoopLevel.Clear();
	}

	public class MixtureGraphProcessor : BaseGraphProcessor
	{
		List<List<BaseNode>>	processLists = new List<List<BaseNode>>();
		new MixtureGraph		graph => base.graph as MixtureGraph;
		HashSet< BaseNode >		executedNodes = new HashSet<BaseNode>();

		Dictionary<CustomRenderTexture, List<BaseNode>> mixtureDependencies = new Dictionary<CustomRenderTexture, List<BaseNode>>();

		public MixtureGraphProcessor(BaseGraph graph) : base(graph)
		{
			// CustomTextureManager.onBeforeCustomTextureUpdated -= BeforeCustomRenderTextureUpdate;
			// CustomTextureManager.onBeforeCustomTextureUpdated += BeforeCustomRenderTextureUpdate;
		}

		// public override void UpdateComputeOrder()
		// {
			
		// }

		// void BeforeCustomRenderTextureUpdate(CommandBuffer cmd, CustomRenderTexture crt)
		// {
		// 	if (mixtureDependencies.TryGetValue(crt, out var dependencies))
		// 	{
		// 		// Update the dependencies of the CRT
		// 		foreach (var nonCRTDep in dependencies)
		// 		{
		// 			// Make sure we don't execute multiple times the same node if there are multiple dependencies that needs it:
		// 			if (executedNodes.Contains(nonCRTDep))
		// 				continue;

		// 			executedNodes.Add(nonCRTDep);
		// 			ProcessNode(cmd, nonCRTDep);
		// 		}
		// 	}
		// }

		static CommandBuffer currentCmd;
		public static void AddGPUAndCPUBarrier()
		{
			Graphics.ExecuteCommandBuffer(currentCmd);
			currentCmd.Clear();
		}

		public static bool isProcessing;

		public override void Run()
		{
			isProcessing = true;
			mixtureDependencies.Clear();
			// HashSet<BaseNode> nodesToBeProcessed = new HashSet<BaseNode>();
			// Stack<BaseNode> nodeToExecute = new Stack<BaseNode>();
			HashSet<ForeachStart> starts = new HashSet<ForeachStart>();
			HashSet<ForeachEnd> ends = new HashSet<ForeachEnd>();
			HashSet<INeedLoopReset> iNeedLoopReset = new HashSet<INeedLoopReset>();
			Stack<(ForeachStart node, int index)> jumps = new Stack<(ForeachStart, int)>();

			UpdateComputeOrder();

			// processList = graph.nodes.Where(n => n.computeOrder != -1).OrderBy(n => n.computeOrder).ToList();

			currentCmd = new CommandBuffer { name = "Mixture" };

			int maxLoopCount = 0;
			foreach (var processList in processLists)
			{
				jumps.Clear();
				starts.Clear();
				ends.Clear();
				iNeedLoopReset.Clear();

				for (int executionIndex = 0; executionIndex < processList.Count; executionIndex++)
				{
					maxLoopCount++;
					if (maxLoopCount > 10000)
					{
						return;
					}

					var node = processList[executionIndex];

					if (node is ForeachStart fs)
					{
						if (!starts.Contains(fs))
						{
							fs.PrepareNewIteration();
							jumps.Push((fs, executionIndex));
							starts.Add(fs);
						}
					}

					bool finalIteration = false;
					if (node is ForeachEnd fe)
					{
						if (!ends.Contains(fe))
						{
							fe.PrepareNewIteration();
							ends.Add(fe);
						}

						if (jumps.Count == 0)
						{
							Debug.Log("Aborted execution, foreach end without start");
							return ;
						}
						var jump = jumps.Peek();

						// Jump back to the foreach start
						if (!jump.node.IsLastIteration())
							executionIndex = jump.index - 1;
						else
						{
							var fs2 = jumps.Pop();
							starts.Remove(fs2.node);
							ends.Remove(fe);
							finalIteration = true;
						}
					}

					if (node is INeedLoopReset i)
					{
						if (!iNeedLoopReset.Contains(i))
						{
							i.PrepareNewIteration();
							iNeedLoopReset.Add(i);
						}

						// TODO: remove this node form iNeedLoopReset when we go over a foreach start again
					}

					ProcessNode(currentCmd, node);
				
					if (finalIteration && node is ForeachEnd fe2)
					{
						fe2.FinalIteration();
					}
				}
			}

			// foreach (var p in processList)
			// 	nodeToExecute.Push(p);

			// while (nodeToExecute.Count > 0)
			// {
			// 	var node = nodeToExecute.Pop();

			// 	ProcessNode(cmd, node);
			// 	if (node is ForeachStart fs)
			// 	{
			// 		if (!starts.Contains(fs))
			// 		{
			// 			// Gather nodes to execute multiple times:
			// 			var nodes = fs.GatherNodesInLoop();
			// 			var it = fs.PrepareNewIteration();
			// 			Debug.Log("Mixture feature it count: " + it);
			// 			foreach (var n in nodes)
			// 				Debug.Log(n);
			// 			for (int i = 0; i < it; i++)
			// 			{
			// 				foreach (var n in nodes)
			// 					nodeToExecute.Push(n);
			// 			}
			// 		}
			// 		starts.Add(fs);
			// 	}
			// }

			// For now we process every node multiple time,
			// future improvement: only refresh nodes when  asked by the CRT
			// foreach (BaseNode node in processList)
			// {
			// 	ProcessNode(cmd, node);
			// 	// if (node is IUseCustomRenderTextureProcessing iUseCRT)
			// 	// {
			// 	// 	var mixtureNode = node as MixtureNode;
			// 	// 	var crt = iUseCRT.GetCustomRenderTexture();

			// 	// 	if (crt != null)
			// 	// 	{
			// 	// 		crt.Update();
			// 	// 		CustomTextureManager.UpdateCustomRenderTexture(cmd, crt);
			// 	// 		// CustomTextureManager.RegisterNewCustomRenderTexture(crt);
			// 	// 		// var deps = mixtureNode.GetMixtureDependencies();
			// 	// 		// foreach (var dep in deps)
			// 	// 		// 	nodesToBeProcessed.Add(dep);
			// 	// 		// mixtureDependencies.Add(crt, deps);
			// 	// 		// crt.Update();
			// 	// 	}
			// 	// }
			// }

			// executedNodes.Clear();
			// CustomTextureManager.ForceUpdateNow();

			// // update all nodes that are not depending on a CRT
			// foreach (var node in processList.Except(nodesToBeProcessed))
			// 	ProcessNode(cmd, node);
			Graphics.ExecuteCommandBuffer(currentCmd);
			isProcessing = false;
		}

		void ProcessNode(CommandBuffer cmd, BaseNode node)
		{
			if (node is MixtureNode m)
			{
				m.OnProcess(cmd);
				if (node is IUseCustomRenderTextureProcessing iUseCRT)
				{
                    var crt = iUseCRT.GetCustomRenderTexture();

                    if (crt != null)
                    {
                        crt.Update();
                        CustomTextureManager.UpdateCustomRenderTexture(cmd, crt);
                    }
				}
			}
			else
				node.OnProcess();
		}

		public override void UpdateComputeOrder()
		{
			// Find graph outputs:
			HashSet<BaseNode> outputs = new HashSet<BaseNode>();
			foreach (var node in graph.nodes)
			{
				if (node.GetOutputNodes().Count() == 0)
					outputs.Add(node);
				node.computeOrder = 1;
			}

			processLists.Clear();

			info.Clear();

			Stack<BaseNode> dfs = new Stack<BaseNode>();
			foreach (var output in outputs)
			{
				dfs.Push(output);
				int index = 0;

				var lst = new HashSet<BaseNode>();

				while (dfs.Count > 0)
				{
					var node = dfs.Pop();

					node.computeOrder = Mathf.Min(node.computeOrder, index);
					index--;

					foreach (var dep in node.GetInputNodes())
						dfs.Push(dep);
					
					lst.Add(node);
				}

				processLists.Add(lst.Where(n => n.computeOrder != 1).OrderBy(n => n.computeOrder).ToList());
			}

			foreach (var processList in processLists)
			{
				int foreachIndex = 0;
				foreach (var node in processList)
				{
					if (node is ForeachStart fs)
						foreachIndex++;
					info.foreachLoopLevel[node] = foreachIndex;
					if (node is ForeachEnd fe)
						foreachIndex--;
				}
			}

			// foreach (var processList in processLists)
			// 	foreach (var p in processList)
			// 		Debug.Log(p + " | " + p.computeOrder);
		}

		ComputeOrderInfo info = new ComputeOrderInfo();
		public ComputeOrderInfo GetComputeOrderInfo()
		{
			return info;
		}
	}
}