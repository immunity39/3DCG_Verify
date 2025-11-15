Shader "Custom/SolderDisplacement"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0.9
        _Glossiness ("Smoothness", Range(0,1)) = 0.8
        
        // (ステップ3) ハンダの膨張量をC#から受け取る
        _SolderAmount ("Solder Amount (from C#)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Standard PBR lighting model, and vertex modification function
        #pragma surface surf Standard fullforwardshadows vertex:vert

        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        
        // C#から受け取るハンダ量
        float _SolderAmount;

        // (ステップ3) 頂点シェーダー関数
        void vert (inout appdata_full v)
        {
            // 1. この頂点の法線ベクトルを取得 (v.normal)
            // 2. C#から受け取った _SolderAmount を使って、
            //    頂点を法線方向に押し出す（膨張させる）
            v.vertex.xyz += v.normal * _SolderAmount;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Standard Surface Shader の基本設定
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
