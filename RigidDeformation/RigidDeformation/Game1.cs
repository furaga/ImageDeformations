using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace RigidDeformation
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        const int GW = 50;
        const int GH = 50;

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch = null;

        DynamicVertexBuffer texVertexBuffer = null;
        DynamicVertexBuffer dotVertexBuffer = null;
        Texture2D tex, dot;

        /// <summary>
        /// 基本エフェクト
        /// </summary>
        private BasicEffect basicEffect = null;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;

            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            // テクスチャーを描画するためのスプライトバッチクラスを作成します
            this.spriteBatch = new SpriteBatch(this.GraphicsDevice);


            // エフェクトを作成
            this.basicEffect = new BasicEffect(this.GraphicsDevice);
            this.basicEffect.TextureEnabled = true;
            this.basicEffect.VertexColorEnabled = true;

            // ビューマトリックスをあらかじめ設定 ((0, 0, 12) から原点を見る)
            this.basicEffect.View = Matrix.CreateLookAt(
                    new Vector3(0.0f, 0.0f, -2.0f),
                    Vector3.Zero,
                    Vector3.Up
                );

            // プロジェクションマトリックスをあらかじめ設定
            this.basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                    MathHelper.ToRadians(45.0f),
                    (float)this.GraphicsDevice.Viewport.Width /
                        (float)this.GraphicsDevice.Viewport.Height,
                    1.0f,
                    100.0f
                );


            tex = Content.Load<Texture2D>("pictogram_man");
            dot = Content.Load<Texture2D>("WhitePixel");
            basicEffect.Texture = tex;

            InitVertices();

        }

        KeyboardState prev_ks;

        protected override void Update(GameTime gameTime)
        {
            Window.Title = "変形タイプ: " + mlsType.ToString() + " [Enterキーで切り替え]";
            KeyboardState ks = Keyboard.GetState();

            if (ks.IsKeyDown(Keys.Escape)) Exit();

            if (prev_ks.IsKeyUp(Keys.Enter) && ks.IsKeyDown(Keys.Enter))
            {
                mlsType = (MLSType)((int)(mlsType + 1) % Enum.GetValues(typeof(MLSType)).Length);
                PrecomputeMLS();
                MLS();
            }
            if (prev_ks.IsKeyUp(Keys.Q) && ks.IsKeyDown(Keys.Q))
            {
                for (int i = 0; i < prev_cps.Length; i++)
                {
                    prev_cps[i] = cps[i] = Vector3.Zero;
                    cps_cnt = 0;
                }
                ResetVertices();
            }

            UpdateControlePoints(gameTime);
            SetVertices(gameTime);

            prev_ks = ks;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            this.GraphicsDevice.SetVertexBuffer(this.texVertexBuffer);
            foreach (EffectPass pass in this.basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                for (int y = 0; y < GH - 1; y++)
                {
                    this.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 2 * GW * y, 2 * (GW - 1));
                }
                for (int i = 0; i < cps_cnt; i++)
                {
                    this.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, texVertexSize + 4 * i, 2);
                }
            }

            base.Draw(gameTime);
        }

        //-----------------------------------------------------------


        // 制御点
        const int texVertexSize = 2 * GW * (GH - 1);
        Vector3[] cps = new Vector3[MAX_CPS];
        Vector3[] vertices = new Vector3[GW * GH];
        VertexPositionColorTexture[] triangleStripVertices = new VertexPositionColorTexture[texVertexSize + 4 * MAX_CPS];

        void InitVertices()
        {
            // TriangleStrip 用頂点バッファ作成
            this.texVertexBuffer = new DynamicVertexBuffer(this.GraphicsDevice,
                VertexPositionColorTexture.VertexDeclaration, texVertexSize + 4 * MAX_CPS, BufferUsage.None);

            for (int y = 0; y < GH; y++)
            {
                for (int x = 0; x < GW; x++)
                {
                    int i = x + y * GW;
                    Vector2 coord = new Vector2(1 - (float)x / GW, (float)y / GH);
                    if (y >= 1)
                    {
                        triangleStripVertices[1 + 2 * x + 2 * GW * (y - 1)] = new VertexPositionColorTexture(Vector3.Zero, Color.White, coord);
                    }
                    if (y <= GH - 2)
                    {
                        triangleStripVertices[2 * x + 2 * GW * y] = new VertexPositionColorTexture(Vector3.Zero, Color.White, coord);
                    }
                }
            }

            for (int i = texVertexSize; i < triangleStripVertices.Length; i++)
            {
                triangleStripVertices[i] = new VertexPositionColorTexture(Vector3.Zero, Color.Red, Vector2.Zero);
            }

            ResetVertices();
        }

        void ResetVertices()
        {
            // TODO: 制御点にしたがって頂点位置を計算
            float ratio = (float)tex.Width / tex.Height;
            if (cps_cnt <= 0)
            {
                for (int y = 0; y < GH; y++)
                {
                    for (int x = 0; x < GW; x++)
                    {
                        int i = x + y * GW;
                        vertices[i] = new Vector3((float)x / GW, (float)y / GH, 0);
                        float vx = vertices[i].X - 0.5f;
                        vx *= ratio;
                        float vy = -(vertices[i].Y - 0.5f);
                        vertices[i] = new Vector3(vx, vy, 0);
                        prev_vertices[i] = new Vector3(vx, vy, 0);
                    }
                }
            }
        }

        Vector3[] prev_cps = new Vector3[MAX_CPS];
        const int MAX_CPS = 100;
        Vector3[] prev_vertices = new Vector3[GW * GH];

        void SetVertices(GameTime gameTime)
        {
            // TODO: 制御点にしたがって頂点位置を計算
            float ratio = (float)tex.Width / tex.Height;
            for (int y = 0; y < GH; y++)
            {
                for (int x = 0; x < GW; x++)
                {
                    int i = x + y * GW;
                    float vx = vertices[i].X;
                    float vy = vertices[i].Y;
                    Vector3 pos = new Vector3(vx, vy, 0);
                    if (y >= 1)
                    {
                        triangleStripVertices[1 + 2 * x + 2 * GW * (y - 1)].Position = pos;
                    }
                    if (y <= GH - 2)
                    {
                        triangleStripVertices[2 * x + 2 * GW * y].Position = pos;
                    }
                }
            }

            //----------------------------------------------------

            // 頂点データを頂点バッファに書き込む
            for (int i = 0; i < cps_cnt; i++)
            {
                triangleStripVertices[texVertexSize + 4 * i].Position = cps[i] + new Vector3(-halfCPSize, halfCPSize, -0.0001f);
                triangleStripVertices[texVertexSize + 4 * i + 1].Position = cps[i] + new Vector3(-halfCPSize, -halfCPSize, -0.0001f);
                triangleStripVertices[texVertexSize + 4 * i + 2].Position = cps[i] + new Vector3(halfCPSize, halfCPSize, -0.0001f);
                triangleStripVertices[texVertexSize + 4 * i + 3].Position = cps[i] + new Vector3(halfCPSize, -halfCPSize, -0.0001f);
                if (i == movingCP)
                {
                    triangleStripVertices[texVertexSize + 4 * i].Color =
                    triangleStripVertices[texVertexSize + 4 * i + 1].Color =
                    triangleStripVertices[texVertexSize + 4 * i + 2].Color =
                    triangleStripVertices[texVertexSize + 4 * i + 3].Color = Color.Red;
                }
                else
                {
                    triangleStripVertices[texVertexSize + 4 * i].Color =
                    triangleStripVertices[texVertexSize + 4 * i + 1].Color =
                    triangleStripVertices[texVertexSize + 4 * i + 2].Color =
                    triangleStripVertices[texVertexSize + 4 * i + 3].Color = Color.Black;
                }
            }
            this.texVertexBuffer.SetData(triangleStripVertices, 0, triangleStripVertices.Length, SetDataOptions.Discard);
        }

        //--------------------------------------------------------------------------

        enum MLSType { Affine, Similarity, Rigid }
        MLSType mlsType = MLSType.Similarity;

        const double ALPHA = 2;
        float[] A;
        float[] W;
        const int MIN_CPS = 2;

        void PrecomputeMLS()
        {
            switch (mlsType)
            {
                case MLSType.Affine:
                    PrecomputeAffineMLS();
                    break;
                case MLSType.Similarity:
                    PrecompSimilarityMLS();
                    break;
                case MLSType.Rigid:
                    PrecompRigidMLS();
                    break;
            }
        }

        void MLS()
        {
            switch (mlsType)
            {
                case MLSType.Affine:
                    AffineMLS();
                    break;
                case MLSType.Similarity:
                    SimilartiyMLS();
                    break;
                case MLSType.Rigid:
                    RigidMLS();
                    break;
            }
        }

        //---------------------------------------------------------------
        // アフィン変換
        //

        void PrecomputeAffineMLS()
        {
            if (cps_cnt < MIN_CPS) return;

            A = new float[GW * GH * MAX_CPS];
            W = new float[GW * GH * MAX_CPS];

            for (int y = 0; y < GH; y++)
            {
                for (int x = 0; x < GW; x++)
                {
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        // 頂点が、各制御点にどれくらい影響を受けるか計算
                        W[idx] = 1.0f / (0.01f + (float)Math.Pow((prev_cps[i] - vertices[x + y * GW]).Length(), ALPHA));
                    }

                    Vector3 Pa = CompWeightAvg(prev_cps, W, x, y);

                    float m00 = 0;
                    float m01 = 0;
                    float m10 = 0;
                    float m11 = 0;
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        Vector3 ph = prev_cps[i] - Pa;
                        m00 += ph.X * W[idx] * ph.X;
                        m01 += ph.X * W[idx] * ph.Y;
                        m10 += ph.Y * W[idx] * ph.X;
                        m11 += ph.Y * W[idx] * ph.Y;
                    }

                    // 逆行列
                    float invdet = 1 / (m00 * m11 - m01 * m10);
                    Vector3 lM = new Vector3(m11 * invdet, -m10 * invdet, 0);
                    Vector3 rM = new Vector3(-m01 * invdet, m00 * invdet, 0);

                    Vector3 t = prev_vertices[x + y * GW] - Pa;
                    Vector3 d = new Vector3(Vector3.Dot(t, lM), Vector3.Dot(t, rM), 0);
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        A[idx] = Vector3.Dot(d, prev_cps[i]) * W[idx];
                    }
                }
            }
        }

        void AffineMLS()
        {
            if (cps_cnt < MIN_CPS) return;

            for (int y = 0; y < GH; y++)
            {
                for (int x = 0; x < GW; x++)
                {
                    Vector3 Qa = CompWeightAvg(cps, W, x, y);
                    vertices[x + y * GW] = Qa;
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        vertices[x + y * GW] += (cps[i] - Qa) * A[idx];
                    }
                }
            }
        }

        //-------------------------------------------------------------------------
        // 類似性変換
        //

        float[] A00, A01, A10, A11;
        Vector3[] D;

        void PrecompSimilarityMLS()
        {
            if (cps_cnt < MIN_CPS) return;
            W = new float[GW * GH * MAX_CPS];
            A00 = new float[GW * GH * MAX_CPS];
            A01 = new float[GW * GH * MAX_CPS];
            A10 = new float[GW * GH * MAX_CPS];
            A11 = new float[GW * GH * MAX_CPS];
            D = new Vector3[GW * GH];

            for (int y = 0; y < GH; y++)
            {
                for (int x = 0; x < GW; x++)
                {
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        // 頂点が、各制御点にどれくらい影響を受けるか計算
                        W[idx] = 1.0f / (0.01f + (float)Math.Pow((prev_cps[i] - vertices[x + y * GW]).Length(), ALPHA));
                    }

                    Vector3 Pa = CompWeightAvg(prev_cps, W, x, y);

                    Vector3[] Ph = new Vector3[cps_cnt];
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        Ph[i] = prev_cps[i] - Pa;
                    }

                    float mu = 0;
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        mu += Ph[i].LengthSquared() * W[idx];
                    }

                    D[x + y * GW] = prev_vertices[x + y * GW] - Pa;
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        A00[idx] = W[idx] / mu * Vector3.Dot(Ph[i], D[x + y * GW]);
                        A01[idx] = -W[idx] / mu * Vector3.Dot(Ph[i], Ortho( D[x + y * GW]));
                        A10[idx] = -W[idx] / mu * Vector3.Dot(Ortho(Ph[i]), D[x + y * GW]);
                        A11[idx] = W[idx] / mu * Vector3.Dot(Ortho(Ph[i]), Ortho(D[x + y * GW]));
                    }
                }
            }
        }

        void SimilartiyMLS()
        {
            if (cps_cnt < MIN_CPS) return;

            for (int y = 0; y < GH; y++)
            {
                for (int x = 0; x < GW; x++)
                {
                    Vector3 Qa = CompWeightAvg(cps, W, x, y);
                    vertices[x + y * GW] = Qa;
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        Vector3 Qh = cps[i] - Qa;
                        vertices[x + y * GW].X += Qh.X * A00[idx] + Qh.Y *  A10[idx];
                        vertices[x + y * GW].Y += Qh.X * A01[idx] + Qh.Y *  A11[idx];
                    }
                }
            }

        }

        Vector3 Ortho(Vector3 v) { return new Vector3(-v.Y, v.X, 0); }

        //-------------------------------------------------------------------------
        // リジッド変換
        //

        void PrecompRigidMLS()
        {
            PrecompSimilarityMLS();
        }

        void RigidMLS()
        {
            if (cps_cnt < MIN_CPS) return;

            for (int y = 0; y < GH; y++)
            {
                for (int x = 0; x < GW; x++)
                {
                    Vector3 Qa = CompWeightAvg(cps, W, x, y);
                    vertices[x + y * GW] = Qa;
                    Vector3 f = Vector3.Zero;
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                        Vector3 Qh = cps[i] - Qa;
                        f.X += Qh.X * A00[idx] + Qh.Y * A10[idx];
                        f.Y += Qh.X * A01[idx] + Qh.Y * A11[idx];
                    }
                    vertices[x + y * GW] += f * D[x + y * GW].Length() / (0.01f + f.Length());
                }
            }
        }

        //--------------------------------------------------------------------------

        // p*, q*の計算
        Vector3 CompWeightAvg(Vector3[] C, float[] W, int x, int y)
        {
            Vector3 r = Vector3.Zero;
            int offset = x * MAX_CPS + y * (GW * MAX_CPS);
            float sum = 0;
            for (int i = offset; i < offset + cps_cnt; i++)
            {
                sum += W[i];
            }
            for (int i = 0; i < cps_cnt; i++)
            {
                int idx = i + x * MAX_CPS + y * (GW * MAX_CPS);
                r += C[i] * W[idx];
            }
            return r / sum;
        }

        //--------------------------------------------------------------------------

        MouseState prev_ms;
        const float halfCPSize = 0.01f;
        const float texZ = 0;
        const float near = 1;
        const float far = 100;
        readonly Vector3 camPos = new Vector3(0, 0, -2);

        void UpdateControlePoints(GameTime gameTime)
        {
            MouseState ms = Mouse.GetState();

            Vector3 mousePos = GraphicsDevice.Viewport.Unproject(new Vector3(ms.X, ms.Y, 1), basicEffect.Projection, basicEffect.View, basicEffect.World);
            float t = (0 - camPos.Z) / (mousePos.Z - camPos.Z);
            Vector3 mousePosZ0 = (1 - t) * camPos + t * mousePos;

            // マウスボタンが押された
            if (prev_ms.RightButton == ButtonState.Released && ms.RightButton == ButtonState.Pressed)
            {
                prev_cps[cps_cnt] = mousePosZ0;
                cps[cps_cnt] = mousePosZ0;
                cps_cnt++;
                PrecomputeMLS();
            }

            MoveControlPoints(gameTime, ms, mousePosZ0);
            MLS();

            prev_ms = ms;
        }

        int movingCP = -1;

        void MoveControlPoints(GameTime gameTime, MouseState ms, Vector3 mousePosZ0)
        {
            if (prev_ms.LeftButton == ButtonState.Released && ms.LeftButton == ButtonState.Pressed)
            {
                if (movingCP < 0)
                {
                    for (int i = 0; i < cps_cnt; i++)
                    {
                        const float threshold = 0.01f * 0.01f;
                        if (Vector3.DistanceSquared(mousePosZ0, cps[i]) < threshold)
                        {
                            movingCP = i;
                            break;
                        }
                    }
                }
                else
                {
                    movingCP = -1;
                }
            }

            if (movingCP >= 0)
            {
                cps[movingCP] = mousePosZ0;
            }
        }

        int cps_cnt = 0;
    }
}