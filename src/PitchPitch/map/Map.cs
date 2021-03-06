﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using SdlDotNet.Graphics;

namespace PitchPitch.map
{
    struct View
    {
        public long X;
        public long Y;
        public int Width;
        public int Height;

        public Size Size { get { return new Size(Width, Height); } }
    }

    class Map : IDisposable
    {
        protected MapInfo _mapInfo;
        public MapInfo MapInfo { get { return _mapInfo; } set { _mapInfo = value; } }

        protected MapChipData _chipData;
        public MapChipData ChipData { get { return _chipData; } set { _chipData = value; } }

        protected PitchPitch _parent;
        protected View view;

        protected List<uint[]> _chips = new List<uint[]>();

        /// <summary>列数</summary>
        protected int _columnNum = 0;
        /// <summary>行数</summary>
        protected int _rowNum = 0;
        /// <summary>1画面表示するのに必要な行数</summary>
        protected int _needRowNum = 0;
        /// <summary>1画面表示するのに必要な列数</summary>
        protected int _needColumnNum = 0;

        protected SdlDotNet.Audio.Music _bgm = null;
        public SdlDotNet.Audio.Music Bgm
        {
            get { return _bgm; }
            set
            {
                if (_bgm != null) _bgm.Dispose();
                _bgm = value;
            }
        }
        protected int _bgmVolume = 50;
        public int BgmVolume
        {
            get { return _bgmVolume; }
            set { _bgmVolume = value; }
        }

        #region 色
        protected Color _backColor = Color.LightSalmon;
        public virtual Color BackColor
        {
            get { return _backColor; }
            set { _backColor = value; }
        }
        protected Color _strongColor = Color.Black;
        public virtual Color StrongColor
        {
            get { return _strongColor; }
            set { _strongColor = value; }
        }
        protected Color _foreColor = Color.Red;
        public virtual Color ForeColor
        {
            get { return _foreColor; }
            set { _foreColor = value; }
        }
        #endregion

        protected Surface _startLineSurface = null;
        protected Surface _goalLineSurface = null;

        #region 表示index
        /// <summary>表示しているチップのX方向のオフセット</summary>
        protected double _xOffset = 0;
        /// <summary>表示しているチップのY方向のオフセット</summary>
        protected double _yOffset = 0;
        /// <summary>表示したいチップのX開始インデックス</summary>
        protected int _xFirstIdx = 0;
        /// <summary>表示したいチップのY開始インデックス</summary>
        protected int _yFirstIdx = 0;
        /// <summary>表示したいチップのX終了インデックスの次</summary>
        protected int _xLastIdx = 0;
        /// <summary>表示したいチップのY終了インデックスの次</summary>
        protected int _yLastIdx = 0;
        #endregion

        private double _chipWidthInv;
        private double _chipHeightInv;
        private int _chipWidth;
        private int _chipHeight;

        public virtual void Init(PitchPitch parent, Size viewSize)
        {
            _parent = parent;

            _chipWidth = _chipData.ChipWidth;
            _chipHeight = _chipData.ChipHeight;
            _chipWidthInv = 1 / (double)_chipData.ChipWidth;
            _chipHeightInv = 1 / (double)_chipData.ChipHeight;

            _needRowNum = (int)Math.Ceiling(viewSize.Height / (double)_chipData.ChipHeight) + 1;
            _needColumnNum = (int)Math.Ceiling(viewSize.Width / (double)_chipData.ChipWidth) + 1;
        }

        public virtual bool HasEnd { get { return true; } }
        public virtual long Height
        {
            get { return _rowNum * _chipData.ChipHeight; }
        }
        public virtual long Width
        {
            get { return _columnNum * _chipData.ChipWidth; }
        }

        #region 座標変換
        /// <summary>Chip index から 表示座標への変換</summary>
        protected double convertIdx2VX(int xIdx)
        {
            return xIdx * _chipWidth - view.X;
        }
        /// <summary>Chip index から 表示座標への変換</summary>
        protected double convertIdx2VY(int yIdx)
        {
            return yIdx * _chipHeight - view.Y;
        }

        /// <summary>Chip index から 座標への変換</summary>
        protected double convertIdx2PX(int xIdx)
        {
            return xIdx * _chipWidth + _xOffset;
        }
        /// <summary>Chip index から 座標への変換</summary>
        protected double convertIdx2PY(int yIdx)
        {
            return yIdx * _chipHeight + _yOffset;
        }

        /// <summary>表示座標 から Chip index への変換</summary>
        protected int convertV2IdxX(double x)
        {
            return (int)((x + view.X - _xOffset) * _chipWidthInv);
        }
        /// <summary>表示座標 から Chip index への変換</summary>
        protected int convertV2IdxY(double y)
        {
            return (int)((y + view.Y - _yOffset) * _chipHeightInv);
        }

        /// <summary>座標 から Chip index への変換</summary>
        protected int convertP2IdxX(double x)
        {
            return (int)((x - _xOffset) * _chipWidthInv);
        }
        /// <summary>座標 から Chip index への変換</summary>
        protected int convertP2IdxY(double y)
        {
            return (int)((y - _yOffset) * _chipHeightInv);
        }
        #endregion

        #region MiniMap表示用
        enum RectLoc
        {
            LeftMiddle,
            CenterTop,
            CenterBottom,
            RightMiddle,
        }
        private Size _surfaceSize = Size.Empty;
        private Dictionary<RectLoc, Rectangle> _miniMapMarginRects = new Dictionary<RectLoc, Rectangle>();
        private Rectangle _miniMapRect = Rectangle.Empty;
        protected virtual void updateMiniMapRect(Size size)
        {
            if (_surfaceSize == size) return;

            double cdw = size.Width / (double)_columnNum;
            double cdh = size.Height / (double)_rowNum;
            Rectangle rect = new Rectangle(Point.Empty, size);

            _miniMapMarginRects = new Dictionary<RectLoc, Rectangle>();
            if (cdw > cdh) // 横に余りが出る
            {
                rect.Width = (int)(rect.Height * _columnNum / (double)_rowNum);
                rect.X = (int)(size.Width / 2.0 - rect.Width / 2.0);
                _miniMapMarginRects.Add(RectLoc.LeftMiddle, 
                    new Rectangle(0, rect.Y, rect.X, rect.Height));
                _miniMapMarginRects.Add(RectLoc.RightMiddle,
                    new Rectangle(rect.Right, rect.Y, size.Width - rect.Right, rect.Height));
            }
            else if (cdh > cdw)
            {
                rect.Height = (int)(rect.Width * _rowNum / (double)_columnNum);
                rect.Y = (int)(size.Height / 2.0 - rect.Height / 2.0);
                _miniMapMarginRects.Add(RectLoc.CenterTop,
                    new Rectangle(rect.X, 0, rect.Width, rect.Y));
                _miniMapMarginRects.Add(RectLoc.CenterBottom,
                    new Rectangle(rect.X, rect.Bottom, rect.Width, size.Height - rect.Bottom));
            }
            _surfaceSize = size;
            _miniMapRect = rect;
        }
        #endregion

        public virtual void SetView(View view)
        {
            this.view = view;
            int vb = (int)(view.Y + view.Height);

            _xOffset = view.X % _chipData.ChipWidth;
            _yOffset = view.Y % _chipData.ChipHeight;
            _xFirstIdx = convertP2IdxX(view.X);
            _yFirstIdx = convertP2IdxY(view.Y);
            _xLastIdx = _xFirstIdx + _needColumnNum;
            _yLastIdx = _yFirstIdx + _needRowNum;

            if(_viewChips != null) _viewChips.Clear();
            _viewChips = null;
        }

        private List<Chip> _viewChips = null;
        public virtual IEnumerable<Chip> EnumViewChipData()
        {
            if (_viewChips == null)
            {
                _viewChips = new List<Chip>();

                double[] vpxs = new double[_xLastIdx - _xFirstIdx];
                double[] vpys = new double[_yLastIdx - _yFirstIdx];
                double[] ppxs = new double[_xLastIdx - _xFirstIdx];
                double[] ppys = new double[_yLastIdx - _yFirstIdx];

                double[] mvpys = new double[0];

                int rlen = _chips.Count > 0 ? _chips[0].Length : 0;
                for (int i = _xFirstIdx, s = 0; i < _xLastIdx && i < _chips.Count; i++, s++)
                {
                    if (i < 0) continue;
                    vpxs[s] = convertIdx2VX(i); ppxs[s] = convertIdx2PX(i);
                }
                for (int j = _yFirstIdx, t = 0; j < _yLastIdx && j < rlen; j++, t++)
                {
                    if (j < 0) continue;
                    vpys[t] = convertIdx2VY(j); ppys[t] = convertIdx2PY(j);
                }
                if (vpys.Length == 0 || vpys[0] > 0)
                {
                    List<double> tmp = new List<double>();
                    double y = vpys[0] - _chipData.ChipHeight;
                    do
                    {
                        tmp.Add(y);
                        y -= _chipData.ChipHeight;
                    } while (y >= 0);
                    tmp.Reverse();
                    mvpys = tmp.ToArray();
                }

                for (int i = _xFirstIdx, s = 0; i < _xLastIdx && i < _chips.Count; i++, s++)
                {
                    if (i < 0) continue;

                    foreach (double d in mvpys)
                    {
                        Chip cd = new Chip();
                        cd.XIdx = i;
                        cd.YIdx = int.MinValue;
                        cd.ViewX = vpxs[s];
                        cd.ViewY = d;
                        cd.ChipData = _chipData.WallChip;
                        if (cd.ChipData >= 0 && cd.ChipData < _chipData.Hardness.Length)
                            cd.Hardness = _chipData.Hardness[cd.ChipData];
                        else
                            cd.Hardness = cd.ChipData == 0 ? 0 : 1;

                        _viewChips.Add(cd);
                        yield return cd;
                    }

                    for (int j = _yFirstIdx, t = 0; j < _yLastIdx && j < _chips[i].Length; j++, t++)
                    {
                        if (j < 0) continue;
                        Chip cd = new Chip();
                        cd.XIdx = i;
                        cd.YIdx = j;
                        cd.ViewX = vpxs[s];
                        cd.ViewY = vpys[t];
                        cd.ChipData = _chips[i][j];
                        if (cd.ChipData >= 0 && cd.ChipData < _chipData.Hardness.Length)
                            cd.Hardness = _chipData.Hardness[cd.ChipData];
                        else
                            cd.Hardness = cd.ChipData == 0 ? 0 : 1;

                        _viewChips.Add(cd);
                        yield return cd;
                    }
                }
            }
            else
            {
                foreach (Chip cd in _viewChips)
                {
                    yield return cd;
                }
            }
        }

        #region 描画
        protected virtual void renderBackground(Surface s, Rectangle r)
        {
            s.Fill(r, _backColor);
        }
        protected virtual void renderForeground(Surface s, Rectangle r)
        {
            foreach (Chip cd in this.EnumViewChipData())
            {
                _chipData.Draw(s, cd.ChipData,
                    new Rectangle((int)(r.X + cd.ViewX), (int)(r.Y + cd.ViewY), _chipData.ChipWidth, _chipData.ChipHeight),
                    ChipResizeMethod.Tile);
            }
        }

        protected virtual void renderMiniMapBackground(Surface s, Rectangle r)
        {
            renderBackground(s, r);
        }
        protected virtual void renderMiniMapForeground(Surface s, Rectangle r)
        {
            updateMiniMapRect(r.Size);
            double cdw = _miniMapRect.Width / (double)_columnNum;
            double cdh = _miniMapRect.Height / (double)_rowNum;

            foreach (KeyValuePair<RectLoc, Rectangle> kv in _miniMapMarginRects)
            {
                Color c = BackColor;
                switch (kv.Key)
                {
                    case RectLoc.CenterBottom:
                    case RectLoc.CenterTop:
                        c = ForeColor;
                        break;
                    case RectLoc.LeftMiddle:
                    case RectLoc.RightMiddle:
                        break;
                }
                s.Fill(kv.Value, c);
            }

            int x0, x1;
            for (int i = 0; i < _chips.Count; i++)
            {
                x0 = (int)(i * cdw);
                if (i == _chips.Count - 1) x1 = _miniMapRect.Width;
                else x1 = (int)((i + 1) * cdw);

                int y0, y1;
                for (int j = 0; j < _chips[i].Length; j++)
                {
                    y0 = (int)(j * cdh);
                    if (j == _chips[i].Length - 1) y1 = _miniMapRect.Height;
                    else y1 = (int)((j + 1) * cdh);

                    _chipData.Draw(s, _chips[i][j],
                        new Rectangle(_miniMapRect.X + (int)x0, _miniMapRect.Y + (int)y0, (int)(x1 - x0), (int)(y1 - y0)),
                        ChipResizeMethod.Stretch);
                }
            }
        }
        protected virtual void renderMiniMapViewBox(Surface s, Rectangle r)
        {
            updateMiniMapRect(r.Size);
            double cdw = _miniMapRect.Width / (double)_columnNum;
            double cdh = _miniMapRect.Height / (double)_rowNum;

            Point lt = new Point((int)(_xFirstIdx * cdw) + _miniMapRect.X, (int)(_yFirstIdx * cdh) + _miniMapRect.Y);
            Point rb = new Point((int)(_xLastIdx * cdw) + _miniMapRect.X, (int)((_yLastIdx - 1) * cdh) + _miniMapRect.Y);
            lt.Offset(r.Location);
            rb.Offset(r.Location);
            if (rb.Y - lt.Y > r.Height) rb.Y = lt.Y + r.Height;

            SdlDotNet.Graphics.Primitives.Box viewBox = new SdlDotNet.Graphics.Primitives.Box(lt, rb);
            SdlDotNet.Graphics.Primitives.Box viewBoxIn = new SdlDotNet.Graphics.Primitives.Box(new Point(lt.X + 1, lt.Y + 1), new Point(rb.X - 1, rb.Y - 1));
            s.Draw(viewBox, _strongColor, true, false);
            s.Draw(viewBoxIn, _strongColor, true, false);
        }

        public virtual void Render(Surface s, Rectangle r)
        {
            if (_startLineSurface == null)
            {
#warning パフォーマンスチェック
                using (Surface ts = ResourceManager.LargePFont.Render("START", _foreColor, true))
                {
                    _startLineSurface = new Surface(ts.Width + 20, view.Height, 32, ts.RedMask, ts.GreenMask, ts.BlueMask, ts.AlphaMask);

                    Color[,] tmp = ts.GetColors(new Rectangle(0, 0, ts.Width, ts.Height));
                    for (int i = 0; i < tmp.GetLength(0); i++)
                    {
                        for (int j = 0; j < tmp.GetLength(1); j++)
                        {
                            Color c = tmp[i, j];
                            tmp[i, j] = Color.FromArgb((int)(c.A / 2.0), c.R, c.G, c.B);
                        }
                    }
                    _startLineSurface.Lock();
                    _startLineSurface.SetPixels(new Point(20, (int)(_startLineSurface.Height / 2.0 - ts.Height / 2.0)), tmp);
                    _startLineSurface.Unlock();
                }
                _startLineSurface.Fill(new Rectangle(0, 0, 3, _startLineSurface.Height), Color.FromArgb(128, _foreColor.R, _foreColor.G, _foreColor.B));
                _startLineSurface.Update();
                _startLineSurface.AlphaBlending = true;
            }
            if (_goalLineSurface == null)
            {
                using (Surface ts = ResourceManager.LargePFont.Render("GOAL", _foreColor, true))
                {
                    _goalLineSurface = new Surface(ts.Width + 20, view.Height, 32, ts.RedMask, ts.GreenMask, ts.BlueMask, ts.AlphaMask);
                    Color[,] tmp = ts.GetColors(new Rectangle(0, 0, ts.Width, ts.Height));
                    for (int i = 0; i < tmp.GetLength(0); i++)
                    {
                        for (int j = 0; j < tmp.GetLength(1); j++)
                        {
                            Color c = tmp[i, j];
                            tmp[i, j] = Color.FromArgb((int)(c.A / 2.0), c.R, c.G, c.B);
                        }
                    }
                    _goalLineSurface.Lock();
                    _goalLineSurface.SetPixels(new Point(0, (int)(_goalLineSurface.Height / 2.0 - ts.Height / 2.0)), tmp);
                    _goalLineSurface.Unlock();
                }
                _goalLineSurface.Fill(new Rectangle(_goalLineSurface.Width - 3, 0, 3, _goalLineSurface.Height), Color.FromArgb(128, _foreColor.R, _foreColor.G, _foreColor.B));
                _goalLineSurface.Update();
                _goalLineSurface.AlphaBlending = true;
            }


            renderBackground(s, r);

            if (_startLineSurface.Width >= view.X && view.X + view.Width >= 0)
            {
                s.Blit(_startLineSurface, new Point(
                    (int)(r.X - view.X),
                    (int)(r.Y + view.Height / 2.0 - _startLineSurface.Height / 2.0)));
            }
            if (this.HasEnd)
            {
                if (this.Width >= view.X && view.X + view.Width >= this.Width - _goalLineSurface.Width)
                {
                    s.Blit(_goalLineSurface, new Point(
                        (int)(r.X + this.Width - _goalLineSurface.Width - view.X),
                        (int)(r.Y + view.Height / 2.0 - _goalLineSurface.Height / 2.0)));
                }
            }

            renderForeground(s, r);
        }

        public virtual void RenderMiniMap(Surface s, Rectangle r)
        {
            renderMiniMapBackground(s, r);
            renderMiniMapForeground(s, r);
        }
        public virtual void RenderMiniMapViewBox(Surface s, Rectangle r)
        {
            renderMiniMapViewBox(s, r);
        }
        #endregion

        public virtual double GetDefaultY(double xInView)
        {
            int xidx = convertV2IdxX(xInView);
            if (xidx >= 0 && xidx < _chips.Count)
            {
                uint[] row = _chips[xidx];
                uint prev = row[0]; int inIdx = 0; int outIdx = row.Length - 1;
                for (int i = 0; i < row.Length; i++)
                {
                    uint r = row[i];
                    if (prev > 0 && r == 0)
                    {
                        // 穴に入った
                        inIdx = i;
                    }
                    else if (prev == 0 && r > 0)
                    {
                        // 穴から出た
                        outIdx = i - 1;
                        break;
                    }
                    prev = r;
                }
                double idx = (outIdx + inIdx) / 2.0;
                return convertIdx2PY((int)idx);
            }
            else
            {
                return -1;
            }
        }

        public void LoadMapText(string[] lines, string mapping)
        {
            _chips = new List<uint[]>();
            int width = lines.Length > 0 ? lines[0].Length : 0;
            int height = lines.Length;

            for (int i = 0; i < width; i++) _chips.Add(new uint[height]);

            for (int j = 0; j < height; j++)
            {
                string line = lines[j];
                for (int i = 0; i < width || i < line.Length; i++)
                {
                    char c = line[i];
                    int idx = mapping.IndexOf(c);
                    if (idx < 0) idx = 1;
                    _chips[i][j] = (uint)idx;
                }
            }

            _columnNum = width;
            _rowNum = height;
        }

        public void LoadMapImage(Bitmap mapBmp, Bitmap chipMapping = null)
        {
            _chips = new List<uint[]>();
            Dictionary<Color, uint> mapping = null;

            if (chipMapping != null)
            {
                mapping = new Dictionary<Color, uint>();
                using (Surface ms = new Surface(chipMapping))
                {
                    Color[,] tmp = ms.GetColors(new Rectangle(0, 0, ms.Width, ms.Height));
                    for (int i = 0; i < tmp.GetLength(0); i++)
                    {
                        for (int j = 0; j < tmp.GetLength(1); j++)
                        {
                            Color c = tmp[i, j];
                            uint idx = (uint)(i + j * tmp.GetLength(0));
                            if (mapping.ContainsKey(c)) mapping[c] = idx;
                            else mapping.Add(c, idx);
                        }
                    }
                }
            }

            Color[,] colors;
            using (Surface s = new Surface(mapBmp))
            {
                colors = s.GetColors(new Rectangle(0, 0, mapBmp.Width, mapBmp.Height));
            }

            _columnNum = colors.GetLength(0);
            _rowNum = colors.GetLength(1);

            if (colors == null)
            {
#warning 例外
            }
            else if (mapping != null)
            {
                for (int i = 0; i < _columnNum; i++)
                {
                    uint[] column = new uint[_rowNum];
                    for (int j = 0; j < _rowNum; j++)
                    {
                        Color c = colors[i, j];
                        if (mapping.ContainsKey(c))
                            column[j] = mapping[c];
                        else
                            column[j] = 0;
                    }
                    _chips.Add(column);
                }
            }
            else
            {
                for (int i = 0; i < _columnNum; i++)
                {
                    uint[] column = new uint[_rowNum];
                    for (int j = 0; j < _rowNum; j++)
                    {
                        Color c = colors[i, j];
                        if ((c.ToArgb() & 0x00ffffff) == 0x00ffffff) column[j] = 0;
                        else column[j] = 1;
                    }
                    _chips.Add(column);
                }
            }
        }

        public virtual void Dispose()
        {
            if (_startLineSurface != null) _startLineSurface.Dispose();
            if (_goalLineSurface != null) _goalLineSurface.Dispose();
            if (_chipData != null) _chipData.Dispose();
            if (_bgm != null) _bgm.Dispose();
        }
    }
}
