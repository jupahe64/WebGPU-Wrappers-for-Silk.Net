using Silk.NET.Assimp;
using Silk.NET.Core.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DeferredRendering
{
    static unsafe class ModelHelper
    {
        public static void LoadModel(string file,
            out Span<uint> indices,
            out Span<Vector3> positions,
            out Span<Vector3> normals,
            out Span<Vector3> tangents,
            out Span<Vector2> texCoords)
        {
            var assimp = Assimp.GetApi();

            if(!System.IO.File.Exists(file))
                throw new FileNotFoundException(file);

            Scene* scene = assimp.ImportFile(file, 0);

            if(scene == null)
                throw new Exception($"model could not be loaded");

            LoadModel(scene, out indices, out positions, out normals, out tangents, out texCoords);

            assimp.FreeScene(scene);
        }

        public static void LoadModel(byte[] data, string? formatHint,
            out Span<uint> indices,
            out Span<Vector3> positions,
            out Span<Vector3> normals,
            out Span<Vector3> tangents,
            out Span<Vector2> texCoords)
        {
            var assimp = Assimp.GetApi();

            Scene* scene = assimp.ImportFileFromMemory(data, (uint)data.Length, 0, formatHint);

            if (scene == null)
                throw new Exception($"model could not be loaded");

            LoadModel(scene, out indices, out positions, out normals, out tangents, out texCoords);

            assimp.FreeScene(scene);
        }

        private static void LoadModel(Scene* scene, 
            out Span<uint> indices, 
            out Span<Vector3> positions, 
            out Span<Vector3> normals,
            out Span<Vector3> tangents,
            out Span<Vector2> texCoords)
        {
            var indexList = new List<uint>();


            uint totalNumVertices = 0;
            for (int iMesh = 0; iMesh < scene->MNumMeshes; iMesh++)
                totalNumVertices += scene->MMeshes[iMesh]->MNumVertices;

            positions = new Vector3[totalNumVertices];
            normals = new Vector3[totalNumVertices];
            tangents = new Vector3[totalNumVertices];
            texCoords = new Vector2[totalNumVertices];

            uint accumulatedVertexCount = 0;

            var jobs = new Stack<(nuint node, Matrix4x4 tranform)>();

            jobs.Push(((nuint)scene->MRootNode, scene->MRootNode->MTransformation));

            do
            {
                var (nodePtr, transform) = jobs.Pop();
                var node = (Node*)nodePtr;

                for (int iMesh = 0; iMesh < node->MNumMeshes; iMesh++)
                {
                    var mesh = scene->MMeshes[node->MMeshes[iMesh]];

                    for (int iFace = 0; iFace < mesh->MNumFaces; iFace++)
                    {
                        var face = mesh->MFaces[iFace];

                        uint first = face.MIndices[0];
                        uint previous = face.MIndices[1];

                        uint indexOffset = accumulatedVertexCount;

                        for (int i = 2; i < face.MNumIndices; i++)
                        {
                            indexList.Add(indexOffset + first);
                            indexList.Add(indexOffset + previous);
                            indexList.Add(indexOffset + (previous = face.MIndices[i]));
                        }
                    }

                    {
                        var sourceSpan = new ReadOnlySpan<Vector3>(mesh->MVertices, (int)mesh->MNumVertices);
                        var destSpan = positions[(int)accumulatedVertexCount..];
                        for (int i = 0; i < sourceSpan.Length; i++)
                            destSpan[i] = Vector3.Transform(sourceSpan[i], transform);
                    }

                    {
                        var sourceSpan = new ReadOnlySpan<Vector3>(mesh->MTextureCoords.Element0, (int)mesh->MNumVertices);
                        var destSpan = texCoords[(int)accumulatedVertexCount..];
                        for (int i = 0; i < sourceSpan.Length; i++)
                            destSpan[i] = new(sourceSpan[i].X, sourceSpan[i].Y);
                    }

                    new ReadOnlySpan<Vector3>(mesh->MNormals, (int)mesh->MNumVertices)
                        .CopyTo(normals[(int)accumulatedVertexCount..]);
                    new ReadOnlySpan<Vector3>(mesh->MTangents, (int)mesh->MNumVertices)
                        .CopyTo(tangents[(int)accumulatedVertexCount..]);
                    

                    accumulatedVertexCount += mesh->MNumVertices;
                }

                for (int iChild = 0; iChild < node->MNumChildren; iChild++)
                {
                    var child = node->MChildren[iChild];
                    jobs.Push(((nuint)child, Matrix4x4.Transpose(child->MTransformation) * transform));
                }
            }
            while (jobs.Count > 0);

            indices = CollectionsMarshal.AsSpan(indexList);
        }
    }
}
