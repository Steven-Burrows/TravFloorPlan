using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TravFloorPlan
{
    public partial class MainForm
    {
        private string? _currentPlanPath;

        private void MenuFile_New_Click(object? sender, EventArgs e)
        {
            _objects.Clear();
            _selectedObject = null;
            propertyGrid.SelectedObject = null;
            RefreshPaletteList();
            UpdateSummaryPanel();
            canvasPanel.Invalidate();
            _currentPlanPath = null;
        }

        private void MenuFile_Open_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "TravFloorPlan (*.json)|*.json|All Files (*.*)|*.*" };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var json = File.ReadAllText(ofd.FileName);

                    var plan = JsonSerializer.Deserialize<PlanDto>(json);
                    if (plan != null && plan.Objects != null && plan.Objects.Count > 0)
                    {
                        _objects.Clear();
                        foreach (var d in plan.Objects)
                        {
                            var typeParsed = ObjectTypes.FromName(d.Type) ?? ObjectTypes.Room;
                            var obj = new PlacedObject
                            {
                                Name = d.Name,
                                Type = typeParsed,
                                Rect = new Rectangle(d.X, d.Y, d.Width, d.Height),
                                RotationDegrees = d.RotationDegrees,
                                Mirrored = d.Mirrored,
                                LineWidth = d.LineWidth,
                                LineColor = Color.FromArgb(d.LineColorArgb),
                                BackgroundColor = Color.FromArgb(d.BackgroundColorArgb),
                                GridSizeForArea = _gridSize,
                                HideNorthSide = d.HideNorthSide,
                                HideEastSide = d.HideEastSide,
                                HideSouthSide = d.HideSouthSide,
                                HideWestSide = d.HideWestSide
                            };
                            _objects.Add(obj);
                        }
                    }
                    else
                    {
                        var loadedObjects = JsonSerializer.Deserialize<List<PlacedObject>>(json) ?? new List<PlacedObject>();
                        _objects.Clear();
                        _objects.AddRange(loadedObjects);
                    }

                    _currentPlanPath = ofd.FileName;
                    _selectedObject = null;
                    propertyGrid.SelectedObject = null;
                    RefreshPaletteList();
                    UpdateSummaryPanel();
                    canvasPanel.Invalidate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to open plan: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MenuFile_Save_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPlanPath))
            {
                MenuFile_SaveAs_Click(sender, e);
                return;
            }
            try
            {
                var plan = new PlanDto();
                foreach (var o in _objects)
                {
                    plan.Objects.Add(new PlacedObjectDto
                    {
                        Name = o.Name,
                        Type = o.Type.Name,
                        X = o.Rect.X,
                        Y = o.Rect.Y,
                        Width = o.Rect.Width,
                        Height = o.Rect.Height,
                        RotationDegrees = o.RotationDegrees,
                        Mirrored = o.Mirrored,
                        LineWidth = o.LineWidth,
                        LineColorArgb = o.LineColor.ToArgb(),
                        BackgroundColorArgb = o.BackgroundColor.ToArgb(),
                        HideNorthSide = o.HideNorthSide,
                        HideEastSide = o.HideEastSide,
                        HideSouthSide = o.HideSouthSide,
                        HideWestSide = o.HideWestSide
                    });
                }
                var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_currentPlanPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save plan: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MenuFile_SaveAs_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog { Filter = "TravFloorPlan (*.json)|*.json|All Files (*.*)|*.*" };
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                _currentPlanPath = sfd.FileName;
                MenuFile_Save_Click(sender, e);
            }
        }

        private void MenuFile_Exit_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
