using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using System.IO;
using System;
using UnityEngine.Rendering;

using UnityEngine.Experimental.Rendering;

namespace Mixture
{
    public static class TextureUtils
    {
        static Dictionary< TextureDimension, Texture >  blackTextures = new Dictionary< TextureDimension, Texture >();

        public static Texture GetBlackTexture(TextureDimension dim, int sliceCount = 0)
        {
            Texture blackTexture;

            if (blackTextures.TryGetValue(dim, out blackTexture))
            {
                // We don't cache texture arrays
                if (dim != TextureDimension.Tex2DArray && dim != TextureDimension.Tex2DArray)
                    return blackTexture;
            }

            switch (dim)
            {
                case TextureDimension.Tex2D:
                    blackTexture = Texture2D.blackTexture;
                    break ;
                case TextureDimension.Tex3D:
                    blackTexture = new Texture3D(1, 1, 1, DefaultFormat.HDR, TextureCreationFlags.None);
                    (blackTexture as Texture3D).SetPixels(new []{Color.black});
                    (blackTexture as Texture3D).Apply();
                    break ;
                case TextureDimension.Tex2DArray:
                    blackTexture = new Texture2DArray(1, 1, sliceCount, DefaultFormat.HDR, TextureCreationFlags.None);
                    for (int i = 0; i < sliceCount; i++)
                        (blackTexture as Texture2DArray).SetPixels(new []{Color.black}, i);
                    (blackTexture as Texture2DArray).Apply();
                    break ;
                default: // TextureDimension.Any / TextureDimension.Unknown
                    throw new Exception($"Unable to create black texture for type {dim}");
            }

            blackTextures[dim] = blackTexture;

            return blackTexture;
        }
    }
}