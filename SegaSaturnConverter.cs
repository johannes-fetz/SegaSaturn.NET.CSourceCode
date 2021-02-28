﻿/*
** SegaSaturn.NET
** Copyright (c) 2020-2021, Johannes Fetz (johannesfetz@gmail.com)
** All rights reserved.
**
** Redistribution and use in source and binary forms, with or without
** modification, are permitted provided that the following conditions are met:
**     * Redistributions of source code must retain the above copyright
**       notice, this list of conditions and the following disclaimer.
**     * Redistributions in binary form must reproduce the above copyright
**       notice, this list of conditions and the following disclaimer in the
**       documentation and/or other materials provided with the distribution.
**     * Neither the name of the Johannes Fetz nor the
**       names of its contributors may be used to endorse or promote products
**       derived from this software without specific prior written permission.
**
** THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
** ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
** WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
** DISCLAIMED. IN NO EVENT SHALL Johannes Fetz BE LIABLE FOR ANY
** DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
** (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
** LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
** ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
** (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
** SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SegaSaturn.NET.CSourceCode
{
    public static class SegaSaturnConverter
    {
        public static string ToSourceFile(SegaSaturnTexture texture, bool preprocessorInclusionProtection)
        {
            if (string.IsNullOrWhiteSpace(texture.Name))
                texture.Name = "Unnamed";
            StringBuilder sb = new StringBuilder();

            if (preprocessorInclusionProtection)
            {
                #region Header

                sb.AppendLine("/*");
                sb.AppendLine("   3D Hardcoded image generated by SegaSaturn.NET.Converter");
                sb.AppendLine("*/");
                sb.AppendLine();
                sb.AppendLine($"#ifndef __SPRITE{texture.Name.ToUpperInvariant()}_H__");
                sb.AppendLine($"# define __SPRITE{texture.Name.ToUpperInvariant()}_H__");
                sb.AppendLine();

                #endregion
            }

            #region Bytes

            sb.AppendLine($"static const unsigned short    SpriteData{texture.Name}[] = {{");
            bool isFirstPixel = true;

            for (int y = 0; y < texture.Height; ++y)
            {
                for (int x = 0; x < texture.Width; ++x)
                {
                    if (!isFirstPixel)
                        sb.Append(',');
                    sb.Append(texture.GetPixel(x, y).SaturnHexaString);
                    isFirstPixel = false;
                }
                sb.AppendLine();
            }

            sb.AppendLine("};");
            sb.AppendLine();

            #endregion

            #region jo_img

            sb.AppendLine($"const jo_img               Sprite{texture.Name} = {{");
            sb.AppendLine($"   .width = {texture.Width},");
            sb.AppendLine($"   .height = {texture.Height},");
            sb.AppendLine($"   .data = (unsigned short *)SpriteData{texture.Name}");
            sb.AppendLine("};");
            sb.AppendLine();

            #endregion

            if (preprocessorInclusionProtection)
            {
                #region Footer

                sb.AppendLine($"#endif /* !__SPRITE{texture.Name.ToUpperInvariant()}_H__ */");

                #endregion
            }

            return sb.ToString();
        }

        public static string ToSourceFile(SegaSaturnObjectModel model, bool exportTextures, bool preprocessorInclusionProtection)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                model.Name = "Unnamed";
            StringBuilder sb = new StringBuilder();
            if (preprocessorInclusionProtection)
            {
                #region Header

                sb.AppendLine("/*");
                sb.AppendLine("   3D model generated by SegaSaturn.NET.Converter");
                sb.AppendLine("*/");
                sb.AppendLine();
                sb.AppendLine($"#ifndef __{model.Name.ToUpperInvariant()}_H__");
                sb.AppendLine($"# define __{model.Name.ToUpperInvariant()}_H__");
                sb.AppendLine();

                #endregion
            }

            #region Points

            sb.AppendLine($"static POINT    Point{model.Name}[] =");
            sb.AppendLine("{");
            foreach (SegaSaturnVertex vertex in model.Vertexes)
                sb.AppendLine($"\t{{{vertex.FixedX}, {vertex.FixedY}, {vertex.FixedZ}}},");
            sb.AppendLine("};");
            sb.AppendLine();

            #endregion

            #region Polygons

            sb.AppendLine($"static POLYGON    Polygon{model.Name}[] =");
            sb.AppendLine("{");
            foreach (SegaSaturnQuad quad in model.Quads)
            {
                string normal = $"{{{{{quad.Normal.FixedX}, {quad.Normal.FixedY}, {quad.Normal.FixedZ}}}";
                string vertices = $"{{{quad.Vertices.A}, {quad.Vertices.B}, {quad.Vertices.C}, {quad.Vertices.D}}}}}";
                sb.AppendLine($"\t{normal}, {vertices},");
            }
            sb.AppendLine("};");
            sb.AppendLine();

            #endregion

            #region Attributes

            sb.AppendLine($"static ATTR    Attribute{model.Name}[] =");
            sb.AppendLine("{");
            foreach (SegaSaturnQuad quad in model.Quads)
            {
                sb.AppendLine($"\tATTRIBUTE({(quad.Attributes.PlaneDisplayMode == SegaSaturnPlaneDisplayMode.Dual ? "Dual_Plane" : "Single_Plane")}, {(quad.Attributes.PlaneZSortMode == SegaSaturnPlaneZSortMode.Bfr ? "SORT_BFR" : quad.Attributes.PlaneZSortMode == SegaSaturnPlaneZSortMode.Cen ? "SORT_CEN" : quad.Attributes.PlaneZSortMode == SegaSaturnPlaneZSortMode.Max ? "SORT_MAX" : "SORT_MIN")}, {(quad.Attributes.TextureId.HasValue ? quad.Attributes.TextureId.Value.ToString() : "No_Texture")}, {(quad.Attributes.Color != null ? quad.Attributes.Color.SaturnHexaString : "No_Palet")}, CL32KRGB | No_Gouraud, {(quad.Attributes.UseScreenDoors ? "CL32KRGB | MESHon" : "CL32KRGB | MESHoff")}, {(quad.Attributes.TextureId.HasValue ? "sprNoflip" : "sprPolygon")}, {(quad.Attributes.UseLight ? "UseLight" : "No_Option")}),");
            }
            sb.AppendLine("};");
            sb.AppendLine();

            #endregion

            #region Textures

            if (exportTextures && model.Textures != null && model.Textures.Count > 0)
            {
                foreach (SegaSaturnTexture texture in model.Textures)
                    sb.Append(SegaSaturnConverter.ToSourceFile(texture, false));
                sb.AppendLine(string.Format("static __jo_force_inline void       load_{0}_textures(void)", model.Name.ToLowerInvariant()));
                sb.AppendLine("{");
                sb.AppendLine("\tint\ti;");
                sb.AppendLine("\tint\t\tfirst_sprite_id;");
                sb.AppendLine();
                bool first = true;
                foreach (SegaSaturnTexture texture in model.Textures)
                {
                    if (string.IsNullOrWhiteSpace(texture.Name))
                        texture.Name = "Unnamed";
                    if (first)
                        sb.AppendLine(string.Format("\tfirst_sprite_id = jo_sprite_add(&Sprite{0});", texture.Name));
                    else
                        sb.AppendLine(string.Format("\tjo_sprite_add(&Sprite{0});", texture.Name));
                    first = false;
                }
                sb.AppendLine($"\tfor (i = 0; i < {model.Quads.Count}; ++i)");
                sb.AppendLine($"\t\tAttribute{model.Name}[i].texno += first_sprite_id;");

                sb.AppendLine("}");
                sb.AppendLine();
            }

            #endregion

            #region PDATA

            sb.AppendLine($"jo_3d_mesh    Mesh{model.Name} =");
            sb.AppendLine("{");
            sb.AppendLine("\t.data =");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\tPoint{model.Name},");
            sb.AppendLine($"\t\t{model.Vertexes.Count},");
            sb.AppendLine($"\t\tPolygon{model.Name},");
            sb.AppendLine($"\t\t{model.Quads.Count},");
            sb.AppendLine($"\t\tAttribute{model.Name}");
            sb.AppendLine("\t}");
            sb.AppendLine("};");
            sb.AppendLine();

            #endregion

            if (preprocessorInclusionProtection)
            {
                #region Footer

                sb.AppendLine($"#endif /* !__{model.Name.ToUpperInvariant()}_H__ */");

                #endregion
            }

            return sb.ToString();
        }

        public static string ToSourceFile(IList<SegaSaturnObjectModel> list, bool exportTextures, bool preprocessorInclusionProtection, string filename)
        {
            StringBuilder sb = new StringBuilder();

            string defaultName = Regex.Replace(Path.GetFileNameWithoutExtension(filename), "[^A-Za-z0-9]", "");

            if (preprocessorInclusionProtection)
            {
                #region Header

                sb.AppendLine("/*");
                sb.AppendLine("   3D model generated by SegaSaturn.NET.Converter");
                sb.AppendLine("*/");
                sb.AppendLine();
                sb.AppendLine($"#ifndef __3D_MODEL{defaultName.ToUpperInvariant()}_H__");
                sb.AppendLine($"# define __3D_MODEL{defaultName.ToUpperInvariant()}_H__");
                sb.AppendLine();

                #endregion
            }

            foreach (SegaSaturnObjectModel model in list)
                sb.Append(SegaSaturnConverter.ToSourceFile(model, exportTextures, false));

            #region load_mesh

            if (list.Any(item => item.Textures.Count > 0))
            {
                sb.AppendLine("/* Call this function in you code to load all textures needed by the mesh */");
                sb.AppendLine($"static __jo_force_inline void       load_{defaultName.ToLowerInvariant()}_mesh_textures(void)");
                sb.AppendLine("{");
                foreach (SegaSaturnObjectModel model in list)
                {
                    if (model.Textures.Count > 0)
                        sb.AppendLine($"\tload_{model.Name.ToLowerInvariant()}_textures();");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            #endregion

            #region display_mesh

            if (list.Count > 0)
            {
                sb.AppendLine("/* Call this function in you code to display all objects */");
                sb.AppendLine($"static __jo_force_inline void       display_{defaultName.ToLowerInvariant()}_mesh(void)");
                sb.AppendLine("{");
                for (int i = 0; i < list.Count; ++i)
                {
                    if (i == 1)
                        sb.AppendLine("\t/* Add matrix transformation here like jo_3d_rotate_matrix() */");
                    sb.AppendLine($"\tjo_3d_mesh_draw(&Mesh{list[i].Name});");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            #endregion

            if (preprocessorInclusionProtection)
            {
                #region Footer

                sb.AppendLine($"#endif /* !__3D_MODEL{defaultName.ToUpperInvariant()}_H__ */");

                #endregion
            }

            return sb.ToString();
        }
    }
}
