using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation
{
    /// <summary>
    /// Renders semi-transparent fog walls at devastated chunk boundaries.
    /// Visible from outside as a warning of devastated areas.
    /// Uses the OIT (Order-Independent Transparency) render stage for proper blending.
    /// </summary>
    public class DevastationFogWallRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private SpreadingDevastationModSystem modSystem;

        // Mesh references for uploaded geometry
        private MeshRef wallMeshRef;

        // White texture for untextured rendering (vertex colors provide the color)
        private LoadedTexture whiteTexture;

        // Configuration (updated from config)
        private bool enabled = true;
        private float wallHeight = 128f;
        private float wallOpacity = 0.15f;
        private float fogColorR = 0.55f;
        private float fogColorG = 0.25f;
        private float fogColorB = 0.15f;
        private float maxRenderDistance = 1000f;
        private float fadeStartDistance = 200f;
        private float wallOffset = 0.5f; // Offset to avoid z-fighting

        // Matrixf for transformations
        private Matrixf modelMat = new Matrixf();

        public double RenderOrder => 0.95; // Render late in OIT stage for proper transparency
        public int RenderRange => 0;

        public DevastationFogWallRenderer(ICoreClientAPI capi, SpreadingDevastationModSystem modSystem)
        {
            this.capi = capi;
            this.modSystem = modSystem;

            // Create a simple 1x1 white texture for untextured rendering
            // The vertex colors will provide the actual fog wall color
            CreateWhiteTexture();

            // Create reusable quad mesh for walls
            CreateWallMesh();
        }

        /// <summary>
        /// Creates a simple white texture for use with the standard shader.
        /// This allows vertex colors to control the appearance without the "missing texture" pattern.
        /// </summary>
        private void CreateWhiteTexture()
        {
            // Create a 2x2 white texture from raw BGRA data
            // Using 2x2 instead of 1x1 for better compatibility with texture filtering
            int[] pixels = new int[] {
                unchecked((int)0xFFFFFFFF), // White pixel (BGRA format)
                unchecked((int)0xFFFFFFFF),
                unchecked((int)0xFFFFFFFF),
                unchecked((int)0xFFFFFFFF)
            };

            whiteTexture = new LoadedTexture(capi, 0, 2, 2);
            capi.Render.LoadOrUpdateTextureFromRgba(pixels, false, 0, ref whiteTexture);
        }

        /// <summary>
        /// Updates configuration from server-sent config.
        /// </summary>
        public void UpdateConfig(FogWallConfigPacket config)
        {
            if (config == null) return;

            capi.Logger.Debug($"[FogWall] UpdateConfig called: Enabled={config.Enabled}, was={enabled}");
            enabled = config.Enabled;
            wallHeight = config.Height;
            wallOpacity = config.Opacity;
            fogColorR = config.ColorR;
            fogColorG = config.ColorG;
            fogColorB = config.ColorB;
            maxRenderDistance = config.MaxDistance;
            fadeStartDistance = config.FadeDistance;
            wallOffset = config.ZOffset;

            // Recreate mesh with new colors/opacity
            wallMeshRef?.Dispose();
            CreateWallMesh();
        }

        private void CreateWallMesh()
        {
            // Create a simple vertical quad (will be transformed per-wall)
            // 4 vertices, 6 indices (2 triangles), with UVs and RGBA colors
            MeshData mesh = new MeshData(4, 6, false, true, true, false);

            // Quad dimensions: 1 chunk wide (32 blocks), wallHeight tall
            float chunkSize = 32f;

            // Vertex positions in local space
            // The quad lies on the XY plane, will be rotated for different orientations
            mesh.AddVertex(0, 0, 0, 0, 0);
            mesh.AddVertex(chunkSize, 0, 0, 1, 0);
            mesh.AddVertex(chunkSize, wallHeight, 0, 1, 1);
            mesh.AddVertex(0, wallHeight, 0, 0, 1);

            // Set vertex colors with alpha for transparency
            // Bottom vertices: higher opacity
            // Top vertices: lower opacity (fade out at top for natural look)
            byte r = (byte)(fogColorR * 255);
            byte g = (byte)(fogColorG * 255);
            byte b = (byte)(fogColorB * 255);
            byte bottomAlpha = (byte)(wallOpacity * 255);
            byte topAlpha = (byte)(wallOpacity * 0.1f * 255); // 10% of base opacity at top

            // Vertex 0 (bottom-left)
            mesh.Rgba[0] = r;
            mesh.Rgba[1] = g;
            mesh.Rgba[2] = b;
            mesh.Rgba[3] = bottomAlpha;

            // Vertex 1 (bottom-right)
            mesh.Rgba[4] = r;
            mesh.Rgba[5] = g;
            mesh.Rgba[6] = b;
            mesh.Rgba[7] = bottomAlpha;

            // Vertex 2 (top-right)
            mesh.Rgba[8] = r;
            mesh.Rgba[9] = g;
            mesh.Rgba[10] = b;
            mesh.Rgba[11] = topAlpha;

            // Vertex 3 (top-left)
            mesh.Rgba[12] = r;
            mesh.Rgba[13] = g;
            mesh.Rgba[14] = b;
            mesh.Rgba[15] = topAlpha;

            // Two triangles for the quad (counter-clockwise winding)
            mesh.AddIndex(0);
            mesh.AddIndex(1);
            mesh.AddIndex(2);
            mesh.AddIndex(0);
            mesh.AddIndex(2);
            mesh.AddIndex(3);

            // Upload to GPU
            wallMeshRef = capi.Render.UploadMesh(mesh);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!enabled)
            {
                return;
            }
            if (capi?.World?.Player?.Entity == null) return;
            if (wallMeshRef == null) return;
            if (whiteTexture == null || whiteTexture.TextureId == 0) return;

            var playerPos = capi.World.Player.Entity.Pos.XYZ;
            int playerChunkX = (int)Math.Floor(playerPos.X / 32.0);
            int playerChunkZ = (int)Math.Floor(playerPos.Z / 32.0);

            // Get devastated chunks from mod system
            var devastatedChunks = modSystem.GetClientDevastatedChunkKeys();
            if (devastatedChunks == null || devastatedChunks.Count == 0) return;

            // Use the standard shader for rendering
            IShaderProgram prog = capi.Render.PreparedStandardShader(
                (int)playerPos.X, (int)playerPos.Y, (int)playerPos.Z
            );

            prog.Use();

            // Bind the white texture - vertex colors will provide the fog wall color
            capi.Render.BindTexture2d(whiteTexture.TextureId);

            // Enable depth test but allow blending for transparency
            capi.Render.GLEnableDepthTest();
            capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);

            // Track which chunk boundaries we've already rendered to avoid duplicates
            HashSet<long> renderedBoundaries = new HashSet<long>();

            foreach (long chunkKey in devastatedChunks)
            {
                // Extract chunk coordinates from key
                int chunkX = (int)(chunkKey >> 32);
                int chunkZ = (int)(chunkKey & 0xFFFFFFFF);

                // Calculate chunk world position (corner)
                float worldX = chunkX * 32f;
                float worldZ = chunkZ * 32f;

                // Calculate distance from player to chunk center
                double distX = worldX + 16 - playerPos.X;
                double distZ = worldZ + 16 - playerPos.Z;
                double dist = Math.Sqrt(distX * distX + distZ * distZ);

                // Skip if too far away
                if (dist > maxRenderDistance) continue;

                // Skip if player is inside or very close to this chunk
                if (playerChunkX == chunkX && playerChunkZ == chunkZ) continue;

                // Calculate opacity based on distance
                float distanceFade = 1f;
                if (dist > fadeStartDistance)
                {
                    distanceFade = 1f - (float)((dist - fadeStartDistance) /
                                                 (maxRenderDistance - fadeStartDistance));
                }

                // Also fade when getting close (within 64 blocks)
                if (dist < 64)
                {
                    distanceFade *= (float)(dist / 64.0);
                }

                // Render walls on edges that:
                // 1. Face the player
                // 2. Border a non-devastated chunk (boundary walls only)
                RenderChunkBoundaryWalls(chunkX, chunkZ, playerPos, distanceFade, prog,
                                         devastatedChunks, renderedBoundaries);
            }

            prog.Stop();
        }

        private void RenderChunkBoundaryWalls(int chunkX, int chunkZ, Vec3d playerPos,
                                               float opacity, IShaderProgram prog,
                                               HashSet<long> devastatedChunks,
                                               HashSet<long> renderedBoundaries)
        {
            float worldX = chunkX * 32f;
            float worldZ = chunkZ * 32f;
            float baseY = Math.Max(0, capi.World.SeaLevel - 40); // Start below sea level

            // Direction from chunk center to player
            double playerChunkX = playerPos.X - (worldX + 16);
            double playerChunkZ = playerPos.Z - (worldZ + 16);

            // Check each of the 4 sides
            // Only render if:
            // 1. The adjacent chunk is NOT devastated (this is a boundary)
            // 2. The wall faces the player

            // North wall (Z = chunkZ * 32, facing -Z direction)
            long northKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ - 1);
            if (!devastatedChunks.Contains(northKey) && playerChunkZ < 0)
            {
                long boundaryKey = MakeBoundaryKey(chunkX, chunkZ, 0);
                if (!renderedBoundaries.Contains(boundaryKey))
                {
                    RenderWall(worldX, baseY, worldZ - wallOffset, 0, opacity, prog);
                    renderedBoundaries.Add(boundaryKey);
                }
            }

            // South wall (Z = chunkZ * 32 + 32, facing +Z direction)
            long southKey = DevastatedChunk.MakeChunkKey(chunkX, chunkZ + 1);
            if (!devastatedChunks.Contains(southKey) && playerChunkZ > 0)
            {
                long boundaryKey = MakeBoundaryKey(chunkX, chunkZ, 1);
                if (!renderedBoundaries.Contains(boundaryKey))
                {
                    RenderWall(worldX, baseY, worldZ + 32 + wallOffset, 0, opacity, prog);
                    renderedBoundaries.Add(boundaryKey);
                }
            }

            // West wall (X = chunkX * 32, facing -X direction)
            long westKey = DevastatedChunk.MakeChunkKey(chunkX - 1, chunkZ);
            if (!devastatedChunks.Contains(westKey) && playerChunkX < 0)
            {
                long boundaryKey = MakeBoundaryKey(chunkX, chunkZ, 2);
                if (!renderedBoundaries.Contains(boundaryKey))
                {
                    RenderWall(worldX - wallOffset, baseY, worldZ + 32, 90, opacity, prog);
                    renderedBoundaries.Add(boundaryKey);
                }
            }

            // East wall (X = chunkX * 32 + 32, facing +X direction)
            long eastKey = DevastatedChunk.MakeChunkKey(chunkX + 1, chunkZ);
            if (!devastatedChunks.Contains(eastKey) && playerChunkX > 0)
            {
                long boundaryKey = MakeBoundaryKey(chunkX, chunkZ, 3);
                if (!renderedBoundaries.Contains(boundaryKey))
                {
                    RenderWall(worldX + 32 + wallOffset, baseY, worldZ, 90, opacity, prog);
                    renderedBoundaries.Add(boundaryKey);
                }
            }
        }

        /// <summary>
        /// Creates a unique key for a chunk boundary (chunk + side).
        /// </summary>
        private long MakeBoundaryKey(int chunkX, int chunkZ, int side)
        {
            // Combine chunk coordinates and side into a unique key
            return ((long)chunkX << 34) | ((long)(chunkZ & 0x3FFFFFFF) << 4) | (uint)side;
        }

        private void RenderWall(float x, float y, float z, float rotationY,
                                float opacity, IShaderProgram prog)
        {
            if (opacity < 0.01f) return; // Skip nearly invisible walls

            // Build transformation matrix
            modelMat.Identity();

            // Translate to world position relative to camera
            modelMat.Translate(
                x - capi.World.Player.Entity.CameraPos.X,
                y - capi.World.Player.Entity.CameraPos.Y,
                z - capi.World.Player.Entity.CameraPos.Z
            );

            // Rotate for wall orientation (90 degrees for E/W walls)
            if (rotationY != 0)
            {
                modelMat.RotateY(rotationY * GameMath.DEG2RAD);
            }

            // Apply extra alpha via the shader's rgbaAmbientIn if possible
            // For now, we rely on the vertex colors set during mesh creation

            prog.UniformMatrix("modelMatrix", modelMat.Values);
            prog.UniformMatrix("viewMatrix", capi.Render.CameraMatrixOriginf);

            capi.Render.RenderMesh(wallMeshRef);
        }

        public void Dispose()
        {
            wallMeshRef?.Dispose();
            wallMeshRef = null;
            whiteTexture?.Dispose();
            whiteTexture = null;
        }
    }

    /// <summary>
    /// Network packet for fog wall configuration.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class FogWallConfigPacket
    {
        [ProtoBuf.ProtoMember(1)]
        public bool Enabled = true;

        [ProtoBuf.ProtoMember(2)]
        public float Height = 128f;

        [ProtoBuf.ProtoMember(3)]
        public float Opacity = 0.15f;

        [ProtoBuf.ProtoMember(4)]
        public float ColorR = 0.55f;

        [ProtoBuf.ProtoMember(5)]
        public float ColorG = 0.25f;

        [ProtoBuf.ProtoMember(6)]
        public float ColorB = 0.15f;

        [ProtoBuf.ProtoMember(7)]
        public float MaxDistance = 1000f;

        [ProtoBuf.ProtoMember(8)]
        public float FadeDistance = 200f;

        [ProtoBuf.ProtoMember(9)]
        public float ZOffset = 0.5f;
    }
}
