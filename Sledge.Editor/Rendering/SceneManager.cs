﻿using Sledge.Rendering.Interfaces;
using Sledge.Rendering.OpenGL;

namespace Sledge.Editor.Rendering
{
    public static class SceneManager
    {
        public static IRenderer Renderer { get; private set; }

        static SceneManager()
        {
            Renderer = new OpenGLRenderer();
        }
    }
}