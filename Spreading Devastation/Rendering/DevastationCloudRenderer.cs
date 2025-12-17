using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation.Rendering
{
    /// <summary>
    /// Represents a static cloud/fog volume at a specific world position.
    /// </summary>
    public class StaticCloud
    {
        public Vec3d Position { get; set; }
        public float Size { get; set; } = 50f;

        public StaticCloud(Vec3d position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Client-side renderer for devastation fog/cloud volumes.
    /// Renders localized fog effects at specific world positions.
    /// Uses the standard shader for reliable rendering.
    /// </summary>
    public class DevastationCloudRenderer : IRenderer, IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly List<StaticCloud> clouds = new List<StaticCloud>();

        private MeshRef volumeMeshRef;
        private bool isInitialized;

        /// <summary>
        /// Render order - renders during OIT stage for proper transparency.
        /// </summary>
        public double RenderOrder => 0.4;

        /// <summary>
        /// Not used for custom renderers.
        /// </summary>
        public int RenderRange => 0;

        public DevastationCloudRenderer(ICoreClientAPI capi)
        {
            this.capi = capi ?? throw new ArgumentNullException(nameof(capi));
        }

        /// <summary>
        /// Initializes the renderer. Must be called after construction.
        /// </summary>
        public bool Initialize()
        {
            if (isInitialized) return true;

            try
            {
                CreateVolumeMesh();
                isInitialized = volumeMeshRef != null;

                if (isInitialized)
                {
                    capi.Logger.Notification("[DevastationCloud] Renderer initialized successfully");
                }
                else
                {
                    capi.Logger.Warning("[DevastationCloud] Failed to create mesh");
                }

                return isInitialized;
            }
            catch (Exception ex)
            {
                capi.Logger.Error("[DevastationCloud] Failed to initialize: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Creates the volume mesh using CubeMeshUtil for reliable rendering.
        /// </summary>
        private void CreateVolumeMesh()
        {
            // Use CubeMeshUtil to create a proper cube with all vertex data
            MeshData mesh = CubeMeshUtil.GetCube(
                0.5f, 0.5f, 0.5f,  // half extents (creates 1x1x1 cube centered at origin)
                new Vec3f(0, 0, 0) // center
            );

            // Color the cube with rust color
            // Top faces brighter, bottom darker for depth
            if (mesh.Rgba != null)
            {
                for (int i = 0; i < mesh.RgbaCount; i += 4)
                {
                    int vertIndex = i / 4;
                    if (vertIndex < mesh.VerticesCount && mesh.xyz != null)
                    {
                        float y = mesh.xyz[vertIndex * 3 + 1];

                        if (y > 0) // Top vertices - brighter
                        {
                            mesh.Rgba[i] = 140;     // R
                            mesh.Rgba[i + 1] = 75;  // G
                            mesh.Rgba[i + 2] = 50;  // B
                            mesh.Rgba[i + 3] = 200; // A
                        }
                        else // Bottom vertices - darker
                        {
                            mesh.Rgba[i] = 100;     // R
                            mesh.Rgba[i + 1] = 50;  // G
                            mesh.Rgba[i + 2] = 35;  // B
                            mesh.Rgba[i + 3] = 180; // A
                        }
                    }
                }
            }

            volumeMeshRef = capi.Render.UploadMesh(mesh);

            capi.Logger.Debug("[DevastationCloud] Mesh created: {0} vertices, {1} indices",
                mesh.VerticesCount, mesh.IndicesCount);
        }

        /// <summary>
        /// Adds a cloud/fog volume at the specified position.
        /// </summary>
        public void AddCloud(Vec3d position, float size = 50f)
        {
            clouds.Add(new StaticCloud(position) { Size = size });
            capi.Logger.Debug("[DevastationCloud] Added cloud at {0}, total: {1}", position, clouds.Count);
        }

        /// <summary>
        /// Removes all clouds.
        /// </summary>
        public void ClearClouds()
        {
            clouds.Clear();
            capi.Logger.Debug("[DevastationCloud] Cleared all clouds");
        }

        /// <summary>
        /// Gets the current cloud count.
        /// </summary>
        public int CloudCount => clouds.Count;

        /// <summary>
        /// Called each frame to render the fog volumes.
        /// </summary>
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!isInitialized || clouds.Count == 0 || volumeMeshRef == null)
                return;

            if (capi.World?.Player?.Entity == null)
                return;

            Vec3d playerPos = capi.World.Player.Entity.CameraPos;
            IRenderAPI rapi = capi.Render;

            IStandardShaderProgram shader = rapi.PreparedStandardShader(
                (int)playerPos.X, (int)playerPos.Y, (int)playerPos.Z
            );

            shader.ExtraGlow = 0;
            shader.RgbaFogIn = capi.Ambient.BlendedFogColor;
            shader.FogMinIn = capi.Ambient?.Base?.FogMin?.Value ?? 0;
            shader.FogDensityIn = capi.Ambient?.Base?.FogDensity?.Value ?? 0;

            rapi.GlToggleBlend(true, EnumBlendMode.Standard);
            rapi.GlDisableCullFace();

            foreach (var cloud in clouds)
            {
                double dx = cloud.Position.X - playerPos.X;
                double dy = cloud.Position.Y - playerPos.Y;
                double dz = cloud.Position.Z - playerPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;

                // Cull clouds beyond 500 blocks
                if (distSq > 500 * 500) continue;

                Matrixf modelMatrix = new Matrixf();
                modelMatrix.Identity();
                modelMatrix.Translate((float)dx, (float)dy, (float)dz);
                modelMatrix.Scale(cloud.Size, cloud.Size * 0.4f, cloud.Size); // Flatten vertically

                shader.ModelMatrix = modelMatrix.Values;
                shader.ViewMatrix = rapi.CameraMatrixOriginf;
                shader.ProjectionMatrix = rapi.CurrentProjectionMatrix;

                rapi.RenderMesh(volumeMeshRef);
            }

            rapi.GlEnableCullFace();
            shader.Stop();
        }

        /// <summary>
        /// Cleans up GPU resources.
        /// </summary>
        public void Dispose()
        {
            if (volumeMeshRef != null)
            {
                capi.Render.DeleteMesh(volumeMeshRef);
                volumeMeshRef = null;
            }

            isInitialized = false;
            capi.Logger.Debug("[DevastationCloud] Renderer disposed");
        }
    }
}
