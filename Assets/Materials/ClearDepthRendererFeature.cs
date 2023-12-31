﻿// ScriptableRendererFeature template created for URP 12 and Unity 2021.2
// Made by Alexander Ameye 
// https://alexanderameye.github.io/

using Assets.Materials;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ClearDepthRendererFeature : ScriptableRendererFeature
{
    // References to our pass and its settings.
    ClearDepthRenderPass pass;

    // Gets called every time serialization happens.
    // Gets called when you enable/disable the renderer feature.
    // Gets called when you change a property in the inspector of the renderer feature.
    public override void Create() {
        // Pass the settings as a parameter to the constructor of the pass.
        pass = new ClearDepthRenderPass();
    }

    // Injects one or multiple render passes in the renderer.
    // Gets called when setting up the renderer, once per-camera.
    // Gets called every frame, once per-camera.
    // Will not be called if the renderer feature is disabled in the renderer inspector.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        // Here you can queue up multiple passes after each other.
        renderer.EnqueuePass(pass);
    }
}