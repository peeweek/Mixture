﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GraphProcessor;

using Status = UnityEngine.UIElements.DropdownMenuAction.Status;

public class MixtureToolbar : ToolbarView
{
	public MixtureToolbar(BaseGraphView graphView) : base(graphView) {}

	MixtureGraph			graph => graphView.graph as MixtureGraph;
	new MixtureGraphView	graphView => base.graphView as MixtureGraphView;

	// TODO: move this elsewhere
	static class MixtureUpdater
	{
		static List< MixtureGraphView > views = new List< MixtureGraphView >();
		static MixtureUpdater()
		{
			EditorApplication.update += Update;
		}

		public static void AddGraphToProcess(MixtureGraphView view)
		{
			views.Add(view);
		}

		public static void RemoveGraphToProcess(MixtureGraphView view)
		{
			views.Remove(view);
		}

		public static void Update()
		{
			// TODO: check if view is visible and alive
			foreach (var view in views)
			{
				view.processor.Run();
				view.MarkDirtyRepaint();
			}
		}
	}

	class Styles
	{
		public const string realtimeToggleText = "RealTime";
		public const string processButtonText = "Process";
	}

	protected override void AddButtons()
	{
		// Add the hello world button on the left of the toolbar
		ToggleRealtime(graph.realtimePreview);
		AddToggle(Styles.realtimeToggleText, graph.realtimePreview, ToggleRealtime, left: false);

		bool exposedParamsVisible = graphView.GetPinnedElementStatus< ExposedParameterView >() != Status.Hidden;
		AddToggle("Show Parameters", exposedParamsVisible, (v) => graphView.ToggleView< ExposedParameterView>());
		AddButton("Show In Project", () => EditorGUIUtility.PingObject(graphView.graph));
	}

	void ToggleRealtime(bool state)
	{
		if (state)
		{
			RemoveButton(Styles.processButtonText, false);
			MixtureUpdater.AddGraphToProcess(graphView);
		}
		else
		{
			AddProcessButton();
			MixtureUpdater.RemoveGraphToProcess(graphView);
		}
		graph.realtimePreview = state;
	}

	void AddProcessButton()
	{
		AddButton(Styles.processButtonText, Process, left: false);
	}

	void Process()
	{
		graphView.processor.Run();
	}
}