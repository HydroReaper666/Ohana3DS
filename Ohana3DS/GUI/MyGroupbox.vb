﻿Public Class MyGroupbox
    Inherits GroupBox
    Public Sub New()
        Me.DoubleBuffered = True
        SetStyle(ControlStyles.AllPaintingInWmPaint Or _
            ControlStyles.DoubleBuffer Or _
            ControlStyles.ResizeRedraw Or _
            ControlStyles.UserPaint Or _
            ControlStyles.OptimizedDoubleBuffer Or _
            ControlStyles.SupportsTransparentBackColor, True)
    End Sub
    Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
        Dim Size As SizeF = e.Graphics.MeasureString(Me.Text, Me.Font)
        Dim Text_Size As Size = New Size(Convert.ToInt32(Size.Width) + 8, Convert.ToInt32(Size.Height))

        Dim Border_Rectangle As New Rectangle(0, 0, Me.Width, Me.Height)
        Border_Rectangle.Y = (Border_Rectangle.Y + (Text_Size.Height \ 2))
        Border_Rectangle.Width -= 1
        Border_Rectangle.Height = (Border_Rectangle.Height - (Text_Size.Height \ 2))

        e.Graphics.DrawLine(New Pen(Me.ForeColor), New Point(0, 8), New Point(6, 8))
        e.Graphics.DrawLine(New Pen(Me.ForeColor), New Point(Text_Size.Width, 8), New Point(Border_Rectangle.Width, 8))

        Dim Text_Rectangle As New Rectangle(8, 0, Text_Size.Width, Text_Size.Height)
        e.Graphics.FillRectangle(New SolidBrush(Me.BackColor), Text_Rectangle)
        e.Graphics.DrawString(Me.Text, Me.Font, New SolidBrush(Me.ForeColor), Text_Rectangle)
    End Sub
    Protected Overrides Sub OnLocationChanged(e As EventArgs)
        Me.Refresh()

        MyBase.OnLocationChanged(e)
    End Sub
End Class
