using Silk.NET.Assimp;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Picking
{
    static unsafe class ModelHelper
    {
        public class Model
        {
            public uint[] Indices;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector3[] Tangents;
            public Vector2[] TexCoords;
            public Vector2[] TexCoords2;
        }

        public static IReadOnlyList<Model> LoadModels(string file)
        {
            var assimp = Assimp.GetApi();

            if(!System.IO.File.Exists(file))
                throw new FileNotFoundException(file);

            Scene* scene = assimp.ImportFile(file, 0);

            if(scene == null)
                throw new Exception($"model could not be loaded");

            var models = LoadModels(scene);

            assimp.FreeScene(scene);
            return models;
        }

        public static IReadOnlyList<Model> LoadModels(byte[] data, string? formatHint)
        {
            var assimp = Assimp.GetApi();

            Scene* scene = assimp.ImportFileFromMemory(data, (uint)data.Length, 0, formatHint);

            if (scene == null)
                throw new Exception($"model could not be loaded");

            var models = LoadModels(scene);

            assimp.FreeScene(scene);
            return models;
        }

        private static IReadOnlyList<Model> LoadModels(Scene* scene)
        {
            List<Model> models = new List<Model>();

            var jobs = new Stack<(nuint node, Matrix4x4 tranform)>();

            jobs.Push(((nuint)scene->MRootNode, scene->MRootNode->MTransformation));

            do
            {
                var (nodePtr, transform) = jobs.Pop();
                var node = (Node*)nodePtr;

                for (int iMesh = 0; iMesh < node->MNumMeshes; iMesh++)
                {
                    var mesh = scene->MMeshes[node->MMeshes[iMesh]];

                    var indexList = new List<uint>();
                    var positions = new Vector3[mesh->MNumVertices];
                    var normals = new Vector3[mesh->MNumVertices];
                    var tangents = new Vector3[mesh->MNumVertices];
                    var texCoords = new Vector2[mesh->MNumVertices];
                    var texCoords2 = new Vector2[mesh->MNumVertices];

                    for (int iFace = 0; iFace < mesh->MNumFaces; iFace++)
                    {
                        var face = mesh->MFaces[iFace];

                        uint first = face.MIndices[0];
                        uint previous = face.MIndices[1];

                        for (int i = 2; i < face.MNumIndices; i++)
                        {
                            indexList.Add(first);
                            indexList.Add(previous);
                            indexList.Add((previous = face.MIndices[i]));
                        }
                    }

                    {
                        var sourceSpan = new ReadOnlySpan<Vector3>(mesh->MVertices, (int)mesh->MNumVertices);
                        var destSpan = positions;
                        for (int i = 0; i < sourceSpan.Length; i++)
                            destSpan[i] = Vector3.Transform(sourceSpan[i], transform);
                    }

                    {
                        var sourceSpan = new ReadOnlySpan<Vector3>(mesh->MTextureCoords.Element0, (int)mesh->MNumVertices);
                        var destSpan = texCoords;
                        for (int i = 0; i < sourceSpan.Length; i++)
                            destSpan[i] = new(sourceSpan[i].X, sourceSpan[i].Y);
                    }

                    {
                        var sourceSpan = new ReadOnlySpan<Vector3>(mesh->MTextureCoords.Element1, (int)mesh->MNumVertices);
                        var destSpan = texCoords2;
                        for (int i = 0; i < sourceSpan.Length; i++)
                            destSpan[i] = new(sourceSpan[i].X, sourceSpan[i].Y);
                    }

                    new ReadOnlySpan<Vector3>(mesh->MNormals, (int)mesh->MNumVertices)
                        .CopyTo(normals);
                    new ReadOnlySpan<Vector3>(mesh->MTangents, (int)mesh->MNumVertices)
                        .CopyTo(tangents);
                    models.Add(new Model
                    {
                        Indices = indexList.ToArray(),
                        Positions = positions,
                        Normals = normals,
                        Tangents = tangents,
                        TexCoords = texCoords,
                        TexCoords2 = texCoords2
                    });
                }

                for (int iChild = 0; iChild < node->MNumChildren; iChild++)
                {
                    var child = node->MChildren[iChild];
                    jobs.Push(((nuint)child, Matrix4x4.Transpose(child->MTransformation) * transform));
                }
            }
            while (jobs.Count > 0);

            return models;
        }
    }
}
