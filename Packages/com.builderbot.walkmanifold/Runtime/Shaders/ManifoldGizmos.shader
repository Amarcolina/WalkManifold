Shader "Unlit/ManifoldGizmos" {
    Properties { 
       _ZTint ("Z Tint", Range(0, 1)) = 0.2
       _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "Queue"="Transparent+500" "RenderType"="Opaque" }
        LOD 100

        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 _Color;

            fixed4 frag(v2f i) : SV_Target{
                return  _Color;
            }
            ENDCG
        }

        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Greater
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float _ZTint;
            float4 _Color;

            fixed4 frag(v2f i) : SV_Target {
                return float4(_Color.rgb, _Color.a * _ZTint);
            }
            ENDCG
        }
    }
}
