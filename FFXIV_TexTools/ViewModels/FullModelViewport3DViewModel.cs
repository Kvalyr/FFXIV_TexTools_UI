﻿// FFXIV TexTools
// Copyright © 2020 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using FFXIV_TexTools.Custom;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Wpf.SharpDX.Animations;
using HelixToolkit.Wpf.SharpDX.Model;
using HelixToolkit.Wpf.SharpDX.Model.Scene;
using Newtonsoft.Json;
using SharpDX;
using SharpDX.Direct3D11;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX.Cameras;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.Helpers;
using Color = SharpDX.Color;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;

namespace FFXIV_TexTools.ViewModels
{
    public class FullModelViewport3DViewModel : Viewport3DViewModel
    {

        private readonly FullModelViewModel _modelViewModel;
        private HashSet<string> shownBonesList = new HashSet<string>();
        private XivRace _targetRace;
        public Dictionary<string, DisplayedModelData> shownModels = new Dictionary<string, DisplayedModelData>();

        public FullModelViewport3DViewModel(FullModelViewModel fmvm)
        {
            _modelViewModel = fmvm;
            Title = "";
            SubTitle = "";

            EffectsManager = new CustomEffectsManager();

            Camera = new PerspectiveCamera();

            BackgroundColor = Properties.Settings.Default.BG_Color;
        }


        #region Properties
        /// <summary>
        /// Model Group containing skeleton node
        /// </summary>
        public SceneNodeGroupModel3D ModelGroup { get; } = new SceneNodeGroupModel3D();


        #endregion

        #region Public Methods

        /// <summary>
        /// Updates or Adds the Model to the viewport
        /// </summary>
        /// <param name="model">The TexTools Model</param>
        /// <param name="textureDataDictionary">The textures associated with the model</param>
        /// <param name="item">The item for the model</param>
        /// <param name="modelRace">The race of the model</param>
        /// <param name="targetRace">The target race the model should be</param>
        public void UpdateModel(TTModel model, Dictionary<int, ModelTextureData> textureDataDictionary, IItemModel item, XivRace modelRace, XivRace targetRace)
        {
            _targetRace = targetRace;
            var itemType = $"{item.PrimaryCategory}_{item.SecondaryCategory}";

            // If target race is different than the model race Apply racial deforms
            if (modelRace != targetRace)
            {
                ApplyDeformers(model, itemType, modelRace, targetRace);
            }

            SharpDX.BoundingBox? boundingBox = null;
            ModelModifiers.CalculateTangents(model);

            // Remove any existing models of the same item type
            RemoveModel(itemType);

            var totalMeshCount = model.MeshGroups.Count;

            for (var i = 0; i < totalMeshCount; i++)
            {
                var meshGeometry3D = GetMeshGeometry(model, i);

                var textureData = textureDataDictionary[model.GetMaterialIndex(i)];

                Stream diffuse = null, specular = null, normal = null, alpha = null, emissive = null;

                if (textureData.Diffuse != null && textureData.Diffuse.Length > 0)
                {
                    using (var img = Image.LoadPixelData<Rgba32>(textureData.Diffuse, textureData.Width, textureData.Height))
                    {
                        diffuse = new MemoryStream();
                        img.Save(diffuse, new PngEncoder());
                    }

                    streamList.Add(diffuse);
                }

                if (textureData.Specular != null && textureData.Specular.Length > 0)
                {
                    using (var img = Image.LoadPixelData<Rgba32>(textureData.Specular, textureData.Width, textureData.Height))
                    {
                        specular = new MemoryStream();
                        img.Save(specular, new PngEncoder());
                    }

                    streamList.Add(specular);
                }

                if (textureData.Normal != null && textureData.Normal.Length > 0)
                {
                    using (var img = Image.LoadPixelData<Rgba32>(textureData.Normal, textureData.Width, textureData.Height))
                    {
                        normal = new MemoryStream();
                        img.Save(normal, new PngEncoder());
                    }

                    streamList.Add(normal);
                }

                if (textureData.Alpha != null && textureData.Alpha.Length > 0)
                {
                    using (var img = Image.LoadPixelData<Rgba32>(textureData.Alpha, textureData.Width, textureData.Height))
                    {
                        alpha = new MemoryStream();
                        img.Save(alpha, new PngEncoder());
                    }

                    streamList.Add(alpha);
                }

                if (textureData.Emissive != null && textureData.Emissive.Length > 0)
                {
                    using (var img = Image.LoadPixelData<Rgba32>(textureData.Emissive, textureData.Width, textureData.Height))
                    {
                        emissive = new MemoryStream();
                        img.Save(emissive, new PngEncoder());
                    }

                    streamList.Add(emissive);
                }

                var material = new PhongMaterial
                {
                    DiffuseColor = PhongMaterials.ToColor(1, 1, 1, 1),
                    SpecularShininess = 1f,
                    DiffuseMap = diffuse,
                    DiffuseAlphaMap = alpha,
                    SpecularColorMap = specular,
                    NormalMap = normal,
                    EmissiveMap = emissive
                };

                // Geometry that contains skeleton data
                var smgm3d = new CustomBoneSkinMeshGeometry3D
                {
                    Geometry = meshGeometry3D,
                    Material = material,
                    ItemType = itemType,
                    BoneMatrices = GetMatrices(model.Bones, targetRace),
                    BoneList = model.Bones
                };

                // Keep track of what bones are showing in the view
                foreach (var modelBone in model.Bones)
                {
                    if (!shownBonesList.Contains(modelBone))
                    {
                        shownBonesList.Add(modelBone);
                    }
                }

                boundingBox = meshGeometry3D.Bound;

                smgm3d.CullMode = Properties.Settings.Default.Cull_Mode.Equals("None") ? CullMode.None : CullMode.Back;

                Models.Add(smgm3d);
            }

            SpecularShine = 1;

            var center = boundingBox.GetValueOrDefault().Center;

            _lightX = center.X;
            _lightY = center.Y;
            _lightZ = center.Z;

            Light3Direction = new Vector3D(_lightX, _lightY, _lightZ);
            Camera.UpDirection = new Vector3D(0, 1, 0);
            Camera.CameraInternal.PropertyChanged += CameraInternal_PropertyChanged;

            // Add the skeleton node for the target race
            AddSkeletonNode(targetRace);

            // Keep track of the models displayed in the viewport
            shownModels.Add(itemType, new DisplayedModelData{TtModel = model, ItemModel = item, ModelTextureData = textureDataDictionary});
        }

        /// <summary>
        /// Adds the skeleton node to the viewport
        /// </summary>
        /// <param name="targetRace">The race the skeleton will be obtained from</param>
        public void AddSkeletonNode(XivRace targetRace)
        {
            // Clear existing skeleton
            ModelGroup.Dispose();
            ModelGroup.Clear();

            var bones = MakeHelixBones(shownBonesList.ToList(), targetRace);

            var bsmNode = new BoneSkinMeshNode
            {
                Bones = bones.ToArray()
            };

            // Create the skeleton node with the given properties
            var skelNode = bsmNode.CreateSkeletonNode(new DiffuseMaterialCore { DiffuseColor = Color.Red }, "xrayGrid", 0.02f);

            ModelGroup.AddNode(skelNode);

            ModelGroup.SceneNode.Visible = _modelViewModel.ShowSkeleton;
        }

        /// <summary>
        /// Updates all models to the new skeleton
        /// </summary>
        /// <param name="previousRace">The original or previous race of the model</param>
        /// <param name="targetRace">The target race for the skeleton and model</param>
        public void UpdateSkeleton(XivRace previousRace, XivRace targetRace)
        {
            var shownModelList = new List<string>();

            foreach (var model in shownModels)
            {
                shownModelList.Add(model.Key);
            }

            // Apply racial transforms
            // This pretty much replaces every model by deleting and recreating them with the target race deforms
            foreach (var model in shownModelList)
            {
                UpdateModel(shownModels[model].TtModel, shownModels[model].ModelTextureData, shownModels[model].ItemModel, previousRace, targetRace);
            }
        }

        /// <summary>
        /// Updates all models to the new skeleton
        /// </summary>
        /// <param name="previousRace">The original or previous race of the model</param>
        /// <param name="targetRace">The target race for the skeleton and model</param>
        public void UpdateSkin(XivRace race)
        {
            var shownModelList = new List<string>();

            foreach (var model in shownModels)
            {
                shownModelList.Add(model.Key);
            }

            foreach (var model in shownModelList)
            {
                UpdateModel(shownModels[model].TtModel, shownModels[model].ModelTextureData, shownModels[model].ItemModel, race, race);
            }
        }



        /// <summary>
        /// Toggles the skeleton visibility
        /// </summary>
        public void ToggleSkeleton(bool visible)
        {
            if (ModelGroup != null)
            {
                ModelGroup.SceneNode.Visible = visible;
            }
        }

        /// <summary>
        /// Removes a model from the viewport
        /// </summary>
        /// <param name="modelItemType">The item type of the model to remove</param>
        public void RemoveModel(string modelItemType)
        {
            // Determine which models needs to be removed
            var modelsToRemove = new List<CustomBoneSkinMeshGeometry3D>();

            if (!string.IsNullOrEmpty(modelItemType))
            {
                foreach (var displayedModel in Models)
                {
                    var model = displayedModel as CustomBoneSkinMeshGeometry3D;

                    if (modelItemType == model.ItemType)
                    {
                        modelsToRemove.Add(model);

                        shownModels.Remove(modelItemType);
                    }
                }
            }

            foreach (var model in modelsToRemove)
            {
                // Remove the bones associated with this model
                foreach (var bone in model.BoneList)
                {
                    shownBonesList.Remove(bone);
                }

                // Remove the model
                model.Dispose();
                Models.Remove(model);
            }

            // Update the skeleton
            AddSkeletonNode(_targetRace);
        }

        /// <summary>
        /// Clears all models and skeleton
        /// </summary>
        public void ClearAll()
        {
            Models.Clear();
            ModelGroup.Clear();
            shownModels.Clear();
            shownBonesList.Clear();
        }

        /// <summary>
        /// Clean up when window is closed
        /// </summary>
        public void CleanUp()
        {
            ModelGroup.Dispose();
            foreach (var model in Models)
            {
                model.Dispose();
            }

            ClearAll();
        }

        #endregion

        #region Overrides

        public override void UpdateTransparency(bool transparencyEnabled)
        {
            foreach (var model in Models)
            {
                var isBody = ((CustomBoneSkinMeshGeometry3D)model).IsBody;

                if (isBody) continue;

                var material = ((CustomBoneSkinMeshGeometry3D)model).Material as PhongMaterial;

                if (transparencyEnabled)
                {
                    ((CustomBoneSkinMeshGeometry3D)model).IsTransparent = true;
                    material.DiffuseColor = PhongMaterials.ToColor(1, 1, 1, .4f);
                }
                else
                {
                    ((CustomBoneSkinMeshGeometry3D)model).IsTransparent = false;
                    material.DiffuseColor = PhongMaterials.ToColor(1, 1, 1, 1);
                }
            }
        }

        protected override void CameraInternal_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("LookDirection"))
            {
                var camera = sender as PerspectiveCameraCore;

                _light1X = -camera.LookDirection.X;
                _light1Y = -camera.LookDirection.Y;
                _light1Z = -camera.LookDirection.Z;

                Light1Direction = new Vector3D(_light1X, _light1Y, _light1Z);

                _light2X = camera.LookDirection.X;
                _light2Y = camera.LookDirection.Y;
                _light2Z = camera.LookDirection.Z;

                Light2Direction = new Vector3D(_light2X, _light2Y, _light2Z);
            }

            _modelViewModel.ResetLightValues();
            _modelViewModel.FlyoutOpen = false;
        }

        #endregion

        #region Private Methods
        /// <summary>
        /// Gets the Mesh Geometry
        /// </summary>
        /// <remarks>
        /// This is mostly the same as the single model viewport but contains bone data
        /// </remarks>
        /// <param name="model">The model to get the geometry from</param>
        /// <param name="meshGroupId">The mesh group ID</param>
        /// <returns>The Skinned Mesh Geometry</returns>
        private BoneSkinnedMeshGeometry3D GetMeshGeometry(TTModel model, int meshGroupId)
        {
            var group = model.MeshGroups[meshGroupId];

            var mg = new BoneSkinnedMeshGeometry3D
            {
                Positions = new Vector3Collection((int)group.VertexCount),
                Normals = new Vector3Collection((int)group.VertexCount),
                Colors = new Color4Collection((int)group.VertexCount),
                TextureCoordinates = new Vector2Collection((int)group.VertexCount),
                BiTangents = new Vector3Collection((int)group.VertexCount),
                Tangents = new Vector3Collection((int)group.VertexCount),
                Indices = new IntCollection((int)group.IndexCount),
                VertexBoneIds = new List<BoneIds>((int)group.IndexCount)
            };

            var indexCount = 0;
            var vertCount = 0;

            foreach (var p in group.Parts)
            {
                foreach (var v in p.Vertices)
                {

                    // I don't think our current shader actually utilizes this data anyways
                    // but may as well include it correctly.
                    var color = new Color4();
                    color.Red = v.VertexColor[0] / 255f;
                    color.Green = v.VertexColor[1] / 255f;
                    color.Blue = v.VertexColor[2] / 255f;
                    color.Alpha = v.VertexColor[3] / 255f;

                    mg.Positions.Add(v.Position);
                    mg.Normals.Add(v.Normal);
                    mg.TextureCoordinates.Add(v.UV1);
                    mg.Colors.Add(color);
                    mg.BiTangents.Add(v.Binormal);
                    mg.Tangents.Add(v.Tangent);
                }

                foreach (var vertexId in p.TriangleIndices)
                {
                    // Get the bone indices and weights for current index
                    var boneIndices = p.Vertices[vertexId].BoneIds;
                    var boneWeights = p.Vertices[vertexId].Weights;
                    var bw1 = boneWeights[0] / 255f;
                    var bw2 = boneWeights[1] / 255f;
                    var bw3 = boneWeights[2] / 255f;
                    var bw4 = boneWeights[3] / 255f;

                    // Add BoneIds to mesh geometry
                    mg.VertexBoneIds.Add(new BoneIds
                    {
                        Bone1 = boneIndices[0],
                        Bone2 = boneIndices[1],
                        Bone3 = boneIndices[2],
                        Bone4 = boneIndices[3],
                        Weights = new Vector4(bw1, bw2, bw3, bw4)
                    });

                    // Have to bump these to account for merging the lists together.
                    mg.Indices.Add(vertCount + vertexId);
                }

                vertCount += p.Vertices.Count;
                indexCount += p.TriangleIndices.Count;
            }
            return mg;
        }

        /// <summary>
        /// Creates the Bones to be used in the format Helix Toolkit uses
        /// </summary>
        /// <param name="boneList">The list of bones in the model</param>
        /// <param name="targetRace">The target race to get the bones from</param>
        /// <returns>A list of Bone structures used by Helix Toolkit</returns>
        private List<Bone> MakeHelixBones(List<string> boneList, XivRace targetRace)
        {
            // Get the skeleton file for the target race
            var skeletonFile = Directory.GetCurrentDirectory() + $"/Skeletons/c{targetRace.GetRaceCode()}.skel";
            var skeletonData = File.ReadAllLines(skeletonFile);


            // Deserialize the skeleton json and create a dictionary of all bones
            var boneDict = new Dictionary<int, SkeletonData>();

            foreach (var b in skeletonData)
            {
                if (b == "") continue;
                var j = JsonConvert.DeserializeObject<SkeletonData>(b);

                boneDict.Add(j.BoneNumber, j);
            }

            // Add only the bones that are contained in the model including all parent bones
            var bonesInModel = new List<SkeletonData>();

            foreach (var bone in boneDict)
            {
                if (!boneList.Contains(bone.Value.BoneName)) continue;

                bonesInModel.Add(bone.Value);

                AddBones(boneDict, bonesInModel, bone.Value);
            }

            // Create a bone list with the bones for the model in helix toolkit format
            var helixBoneList = new List<Bone>();

            foreach (var bone in bonesInModel)
            {
                var bp = new Matrix(bone.InversePoseMatrix);
                bp.Invert();

                helixBoneList.Add(new Bone
                {
                    BindPose = bp,
                    Name = bone.BoneName,
                    ParentIndex = bone.BoneParent

                });
            }

            return helixBoneList;
        }

        /// <summary>
        /// Adds the parent bones all the way to root using recursive calls
        /// </summary>
        /// <param name="skelDict">Dictionary containing all skeleton data by bone number</param>
        /// <param name="skelData">List containing bones to be used for the model</param>
        /// <param name="bone">The bone being added</param>
        private void AddBones(Dictionary<int, SkeletonData> skelDict, List<SkeletonData> skelData, SkeletonData bone)
        {
            // Determine whether the parent has already been added
            var parentAlreadyAdded = skelData.Any(b => b.BoneNumber == bone.BoneParent);

            // This would be the root bone
            if (bone.BoneParent == -1)
            {
                skelData.Add(bone);
                parentAlreadyAdded = true;
            }
            
            // If the parent has not been added, make a recursive call with the parent bone
            if (!parentAlreadyAdded)
            {
                var parent = skelDict[bone.BoneParent];
                AddBones(skelDict, skelData, parent);

                // Update the bone with the new parent bone index
                var newParent = (from b in skelData where b.BoneName == parent.BoneName select b).FirstOrDefault();
                bone.BoneParent = skelData.IndexOf(newParent);
                skelData.Add(bone);
            }
            // If the parent already exists, and it's not the root bone, just add the bone 
            else if (bone.BoneParent != -1)
            {
                var parent = skelDict[bone.BoneParent];

                // Update the bone with the new parent bone index
                var newParent = (from b in skelData where b.BoneName == parent.BoneName select b).FirstOrDefault();
                bone.BoneParent = skelData.IndexOf(newParent);

                skelData.Add(bone);
            }
        }

        /// <summary>
        /// Gets the matrices for the bones used in the model
        /// </summary>
        /// <param name="boneList">List of bones used in the model</param>
        /// <param name="targetRace">Target Race to get the bone data for</param>
        /// <returns>A matrix array containing the pose data for each bone</returns>
        private Matrix[] GetMatrices(List<string> boneList, XivRace targetRace)
        {
            // Get the skeleton file for the target race
            var skeletonFile = Directory.GetCurrentDirectory() + $"/Skeletons/c{targetRace.GetRaceCode()}.skel";
            var skeletonData = File.ReadAllLines(skeletonFile);

            // Deserialize the skeleton json and create a dictionary of all bones
            var skelData = new Dictionary<string, SkeletonData>();

            foreach (var b in skeletonData)
            {
                if (b == "") continue;
                var j = JsonConvert.DeserializeObject<SkeletonData>(b);

                skelData.Add(j.BoneName, j);
            }

            // Add matrices for bones in the model to a list
            // Add missing bones if they exist in the model but not in the target race
            var matrixList = new List<Matrix>();
            var missingBones = new List<string>();

            foreach (var bone in boneList)
            {
                if (skelData.ContainsKey(bone))
                {
                    var matrix = new Matrix(skelData[bone].InversePoseMatrix);
                    matrix.Invert();

                    matrixList.Add(matrix);
                }
                else
                {
                    missingBones.Add(bone);
                }

            }

            // Show a warning when there is bones in the model that do not exist in the target race skeleton
            // The model will still be added, but with no bones animation will not work for that part
            if (missingBones.Count > 0)
            {
                var warning = new StringBuilder();
                warning.AppendLine();

                foreach (var bone in missingBones)
                {
                    warning.AppendLine(bone);
                }

                FlexibleMessageBox.Show(string.Format(UIMessages.MissingBones, targetRace, warning), UIMessages.MissingBonesTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return matrixList.ToArray();
        }

        /// <summary>
        /// Applies the deformer to a model
        /// </summary>
        /// <param name="model">The model being deformed</param>
        /// <param name="itemType">The item type of the model</param>
        /// <param name="currentRace">The current model race</param>
        /// <param name="targetRace">The target race to convert the model to</param>
        private void ApplyDeformers(TTModel model, string itemType, XivRace currentRace, XivRace targetRace)
        {
            try
            {
                // Current race is already parent node
                // Direct conversion
                // [ Current > (apply deform) > Target ]
                if (currentRace.IsDirectParentOf(targetRace))
                {
                    ModelModifiers.ApplyRacialDeform(model, targetRace);
                }
                // Target race is parent node of Current race
                // Convert to parent (invert deform)
                // [ Current > (apply inverse deform) > Target ]
                else if (targetRace.IsDirectParentOf(currentRace))
                {
                    ModelModifiers.ApplyRacialDeform(model, currentRace, true);
                }
                // Current race is not parent of Target Race and Current race has parent
                // Make a recursive call with the current races parent race
                // [ Current > (apply inverse deform) > Current.Parent > Recursive Call ]
                else if (currentRace.GetNode().Parent != null)
                {
                    ModelModifiers.ApplyRacialDeform(model, currentRace, true);
                    ApplyDeformers(model, itemType, currentRace.GetNode().Parent.Race, targetRace);
                }
                // Current race has no parent
                // Make a recursive call with the target races parent race
                // [ Target > (apply deform on Target.Parent) > Target.Parent > Recursive Call ]
                else
                {
                    ModelModifiers.ApplyRacialDeform(model, targetRace.GetNode().Parent.Race);
                    ApplyDeformers(model, itemType, targetRace.GetNode().Parent.Race, targetRace);
                }
            }
            catch (Exception ex)
            {
                // Show a warning that deforms are missing for the target race
                // This mostly happens with Face, Hair, Tails, Ears, and Female > Male deforms
                // The model is still added but no deforms are applied
                FlexibleMessageBox.Show(string.Format(UIMessages.MissingDeforms, targetRace.GetDisplayName(), itemType, ex.Message), UIMessages.MissingDeformsTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }

        #endregion

        public class DisplayedModelData
        {
            public TTModel TtModel { get; set; }
            public IItemModel ItemModel { get; set; }
            public Dictionary<int, ModelTextureData> ModelTextureData { get; set; }
        }
    }
}