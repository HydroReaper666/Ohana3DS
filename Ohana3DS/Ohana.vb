﻿Imports System.IO
Imports Microsoft.DirectX
Imports Microsoft.DirectX.Direct3D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Globalization

Public Class Ohana
    Public Scale As Single = 32.0F
    Private Device As Device

    Public Structure OhanaVertex
        Dim X, Y, Z As Single
        Dim NX, NY, NZ As Single
        Dim Color As Integer
        Dim U, V As Single

        '---

        Dim Bone_1, Bone_2, Bone_3, Bone_4 As Integer
        Dim Weight_1, Weight_2, Weight_3, Weight_4 As Single
    End Structure
    Public Structure VertexList
        Dim Vertex_Format As Integer
        Dim Texture_ID As Integer
        Dim Vertice() As OhanaVertex
        Dim Index() As Integer
    End Structure
    Public Structure OhanaTexture
        Dim Name As String
        Dim Image As Bitmap
        Dim Image_Mirrored As Bitmap
        Dim Format As Integer
        Dim Texture As Texture
        Dim Has_Alpha As Boolean
    End Structure
    Public Structure OhanaBone
        Dim Name As String
        Dim Parent_ID As Integer
        Dim Translation As Vector3
        Dim Rotation As Vector3
        Dim Scale As Vector3
    End Structure
    Private Enum ModelType
        Character
        Map
    End Enum
    Public Enum BCH_Version
        XY
        ORAS
    End Enum
    Public Model_Object() As VertexList
    Public Model_Texture As List(Of OhanaTexture)
    Public Model_Bone() As OhanaBone
    Public Model_Texture_Index() As String
    Public Model_Bump_Map_Index() As String
    Private Model_Type As ModelType

    Public Structure OhanaInfo
        Dim Vertex_Count As Integer
        Dim Triangles_Count As Integer
        Dim Bones_Count As Integer
        Dim Textures_Count As Integer
    End Structure
    Public Info As OhanaInfo

    Private Total_Vertex As Integer

    Public Zoom As Single = 1.0F
    Public Rotation As Vector2
    Public Translation As Vector2

    Private Tile_Order() As Integer = _
        {0, 1, 8, 9, 2, 3, 10, 11, _
         16, 17, 24, 25, 18, 19, 26, 27, _
         4, 5, 12, 13, 6, 7, 14, 15, _
         20, 21, 28, 29, 22, 23, 30, 31, _
         32, 33, 40, 41, 34, 35, 42, 43, _
         48, 49, 56, 57, 50, 51, 58, 59, _
         36, 37, 44, 45, 38, 39, 46, 47, _
         52, 53, 60, 61, 54, 55, 62, 63}
    Private Modulation_Table(,) As Integer = _
        {{2, 8, -2, -8}, _
        {5, 17, -5, -17}, _
        {9, 29, -9, -29}, _
        {13, 42, -13, -42}, _
        {18, 60, -18, -60}, _
        {24, 80, -24, -80}, _
        {33, 106, -33, -106}, _
        {47, 183, -47, -183}}
    Public Sub Initialize(Handle As IntPtr)
        Dim Present As New PresentParameters
        With Present
            .BackBufferCount = 1
            .BackBufferFormat = Manager.Adapters(0).CurrentDisplayMode.Format
            .BackBufferWidth = 640
            .BackBufferHeight = 480
            .Windowed = True
            .SwapEffect = SwapEffect.Discard
            .EnableAutoDepthStencil = True
            .AutoDepthStencilFormat = DepthFormat.D16
            Dim Samples As MultiSampleType
            For Samples = MultiSampleType.SixteenSamples To MultiSampleType.None Step -1
                If Manager.CheckDeviceMultiSampleType(0, DeviceType.Hardware, Format.D16, True, Samples) Then Exit For
            Next
            .MultiSample = Samples
        End With

        Device = New Device(0, DeviceType.Hardware, Handle, CreateFlags.HardwareVertexProcessing, Present)
        With Device
            .RenderState.Lighting = True
            .Lights(0).Type = LightType.Point
            .Lights(0).Diffuse = Color.White
            .Lights(0).Position = New Vector3(0.0F, 80.0F, 20.0F)
            .Lights(0).Range = 250.0F
            .Lights(0).Attenuation0 = 0.05F
            .Lights(0).Enabled = True

            .RenderState.Ambient = Color.FromArgb(100, 100, 100)
            .RenderState.CullMode = Cull.None
            .RenderState.ZBufferEnable = True
            .RenderState.AlphaBlendEnable = True
            .RenderState.SourceBlend = Blend.SourceAlpha
            .RenderState.DestinationBlend = Blend.InvSourceAlpha
            .RenderState.BlendOperation = BlendOperation.Add

            .SamplerState(0).MinFilter = TextureFilter.Anisotropic
            .SamplerState(0).MagFilter = TextureFilter.Anisotropic
            .SamplerState(0).MaxAnisotropy = 16
        End With
    End Sub
    Public Sub Load_Model(File_Name As String, Optional Version As BCH_Version = BCH_Version.XY)
        Total_Vertex = 0

        Dim Temp() As Byte = File.ReadAllBytes(File_Name)
        Dim File_Magic As String = Nothing
        For i As Integer = 0 To 2
            File_Magic &= Chr(Temp(i))
        Next

        Dim BCH_Offset As Integer
        If Left(File_Magic, 2) = "PC" Then
            BCH_Offset = &H80
            Model_Type = ModelType.Character
        ElseIf File_Magic = "BCH" Then
            BCH_Offset = 0
            Model_Type = ModelType.Character
        ElseIf Left(File_Magic, 2) = "GR" Then
            BCH_Offset = Read32(Temp, 8)
            Model_Type = ModelType.Map
        Else
            Exit Sub
        End If

        Dim Data(Temp.Length - BCH_Offset) As Byte
        Buffer.BlockCopy(Temp, BCH_Offset, Data, 0, Temp.Length - BCH_Offset)

        Dim Bones_Offset As Integer = Read32(Data, 8)
        Dim Texture_Names_Offset As Integer = Read32(Data, &HC)
        Dim Description_Offset As Integer = Read32(Data, &H10)
        Dim Data_Offset As Integer = Read32(Data, &H14)
        Dim Texture_Names_Length As Integer = Read32(Data, &H20)
        Dim Table_Offset As Integer
        If Version = BCH_Version.XY Then
            Table_Offset = &H6C + Read32(Data, &H104)
        ElseIf Version = BCH_Version.ORAS Then
            Table_Offset = &H78 + Read32(Data, &H110)
        End If
        Dim Texture_Entries As Integer = Read32(Data, Table_Offset + 4)
        Dim Bone_Entries As Integer = Read32(Data, Table_Offset + &H40)
        Bones_Offset += Read32(Data, Table_Offset + &H44)
        Dim Texture_Table_Offset As Integer
        Dim Vertex_Table_Offset As Integer = Read32(Data, Table_Offset + &HC)
        Dim Entries As Integer = Read32(Data, Table_Offset + &H10)

        Dim Nodes As New List(Of List(Of Integer))
        If Version = BCH_Version.XY Then
            Dim Node_ID_Offset As Integer = Read32(Data, Table_Offset + &H28) + &H38

            For Node As Integer = 0 To Entries - 1
                Dim Node_Count As Integer = Read16(Data, Node_ID_Offset + 2)
                Nodes.Add(New List(Of Integer))
                For Index As Integer = 0 To Node_Count - 1
                    Nodes(Node).Add(Read16(Data, Node_ID_Offset + 4 + (Index * 2)))
                Next
                Node_ID_Offset += &H34
                If Data(Node_ID_Offset) <> 1 Then Node_ID_Offset += &H48
            Next
        End If

        If Version = BCH_Version.XY Then
            Texture_Table_Offset = &H78 + Read32(Data, Table_Offset)
            Table_Offset = &H38 + Read32(Data, Table_Offset + &H14)
        ElseIf Version = BCH_Version.ORAS Then
            Texture_Table_Offset = &H48 + Read32(Data, Table_Offset)
            Table_Offset = &H44 + Read32(Data, Table_Offset + &H14)
        End If

        Dim Vertex_Offsets As New List(Of Integer)
        If Version = BCH_Version.ORAS Then
            For Entry As Integer = 0 To Entries - 1
                Dim Vertex_Offset As Integer = Description_Offset + Read32(Data, Table_Offset + (Entry * &H38) + 8)
                Vertex_Offsets.Add(Data_Offset + Read32(Data, Vertex_Offset + &H30))
            Next
            Dim Face_Offset As Integer = Read32(Data, Table_Offset + &H10)
            Face_Offset = Description_Offset + Read32(Data, Face_Offset + &H70)
            Vertex_Offsets.Add(Data_Offset + Read32(Data, Face_Offset + &H10))
            Vertex_Offsets.Sort()
        End If

        Dim Texture_ID_List As New List(Of Integer)
        ReDim Model_Object(Entries - 1)
        Dim Vertex_Count As Integer
        For Entry As Integer = 0 To Entries - 1
            Dim Base_Offset As Integer = Table_Offset + (Entry * &H38)
            Dim Texture_ID As Integer = Read32(Data, Base_Offset)
            If Not Texture_ID_List.Contains(Texture_ID) Then Texture_ID_List.Add(Texture_ID)
            Dim Vertex_Offset As Integer = Description_Offset + Read32(Data, Base_Offset + 8)
            Dim Face_Offset As Integer = Read32(Data, Base_Offset + &H10)
            Dim Face_Offset_2 As Integer = Read32(Data, Base_Offset + &H34)
            Dim Face_Offset_3 As Integer = 0

            If Version = BCH_Version.XY Then
                If Read32(Data, Face_Offset + &H78) <> &HFFFFFFFF Then Face_Offset_3 = Description_Offset + Read32(Data, Face_Offset + &H98)
                Face_Offset = Description_Offset + Read32(Data, Face_Offset + &H64)
                Face_Offset_2 = Description_Offset + Read32(Data, Face_Offset_2 + &H30)
            ElseIf Version = BCH_Version.ORAS Then
                If Read32(Data, Face_Offset + &H84) <> &HFFFFFFFF Then Face_Offset_3 = Description_Offset + Read32(Data, Face_Offset + &HA4)
                Face_Offset = Description_Offset + Read32(Data, Face_Offset + &H70)
                Face_Offset_2 = Description_Offset + Read32(Data, Face_Offset_2 + &H3C)
            End If

            Dim Vertex_Data_Offset As Integer = Data_Offset + Read32(Data, Vertex_Offset + &H30)
            Dim Vertex_Data_Format As Integer = Data(Vertex_Offset + &H3A)
            Dim Vertex_Bytes As Integer = Read32(Data, Face_Offset)
            Dim Face_Data_Offset As Integer = Data_Offset + Read32(Data, Face_Offset + &H10)

            Dim Vertex_Data_Length As Integer
            If Version = BCH_Version.XY Then
                Vertex_Data_Length = Face_Data_Offset - Vertex_Data_Offset
            ElseIf Version = BCH_Version.ORAS Then
                Vertex_Data_Length = Vertex_Offsets(Vertex_Offsets.IndexOf(Vertex_Data_Offset) + 1) - Vertex_Data_Offset
            End If

            Dim Count As Integer = Vertex_Data_Length \ Vertex_Data_Format
            ReDim Model_Object(Entry).Vertice(Count - 1)
            Dim Offset As Integer = Vertex_Data_Offset
            Dim Current_Node As Integer = Entry
            For Index As Integer = 0 To Count - 1
                With Model_Object(Entry).Vertice(Index)
                    .X = BitConverter.ToSingle(Data, Offset) / Scale
                    .Y = BitConverter.ToSingle(Data, Offset + 4) / Scale
                    .Z = BitConverter.ToSingle(Data, Offset + 8) / Scale

                    .Color = &HFFFFFFFF
                    Select Case Vertex_Data_Format
                        Case &H10
                            .Color = Read32(Data, Offset + 12)
                        Case &H14, &H1C
                            .U = BitConverter.ToSingle(Data, Offset + 12)
                            .V = BitConverter.ToSingle(Data, Offset + 16)
                        Case &H18
                            .U = BitConverter.ToSingle(Data, Offset + 12)
                            .V = BitConverter.ToSingle(Data, Offset + 16)

                            .Color = Read32(Data, Offset + 20)
                        Case &H20, &H30, &H38
                            .NX = BitConverter.ToSingle(Data, Offset + 12) / Scale
                            .NY = BitConverter.ToSingle(Data, Offset + 16) / Scale
                            .NZ = BitConverter.ToSingle(Data, Offset + 20) / Scale

                            .U = BitConverter.ToSingle(Data, Offset + 24)
                            .V = BitConverter.ToSingle(Data, Offset + 28)
                        Case &H24, &H28, &H2C
                            .NX = BitConverter.ToSingle(Data, Offset + 12) / Scale
                            .NY = BitConverter.ToSingle(Data, Offset + 16) / Scale
                            .NZ = BitConverter.ToSingle(Data, Offset + 20) / Scale

                            .U = BitConverter.ToSingle(Data, Offset + 24)
                            .V = BitConverter.ToSingle(Data, Offset + 28)

                            If Vertex_Data_Format = &H24 Then
                                Select Case Vertex_Bytes And &HFFFF
                                    Case &H8E81, &H8E82, &HAA83, &HAADB, &HAC81, &HAE83
                                        .Color = Read32(Data, Offset + 32)
                                End Select
                            ElseIf Vertex_Data_Format = &H28 Then
                                If Version = BCH_Version.XY Then
                                    If Nodes(Current_Node).Count < 5 Then .Color = Read32(Data, Offset + 32)
                                End If
                            Else
                                Select Case Vertex_Bytes And &HFFFF
                                    Case &HAA83, &HAB83, &HAE83, &HAF83, &HEF83, &HEFD3
                                        .Color = Read32(Data, Offset + 32)
                                End Select
                            End If
                        Case &H34
                    .NX = BitConverter.ToSingle(Data, Offset + 12) / Scale
                    .NY = BitConverter.ToSingle(Data, Offset + 16) / Scale
                    .NZ = BitConverter.ToSingle(Data, Offset + 20) / Scale

                    .U = BitConverter.ToSingle(Data, Offset + 24)
                    .V = BitConverter.ToSingle(Data, Offset + 28)

                    If Version = BCH_Version.XY Then
                        If Nodes(Current_Node).Count < 5 Then .Color = Read32(Data, Offset + 44)
                    End If
                    End Select

                    If Bone_Entries > 0 And Version = BCH_Version.XY Then
                        Select Case Vertex_Data_Format
                            Case &HC, &H10, &H14, &H18, &H20
                                .Bone_1 = Nodes(Current_Node)(0)

                                .Weight_1 = 1
                            Case &H1C
                                If Data(Offset + 20) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 20))
                                If Data(Offset + 21) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 21))
                                If Data(Offset + 22) < Nodes(Current_Node).Count Then .Bone_3 = Nodes(Current_Node)(Data(Offset + 22))
                                If Data(Offset + 23) < Nodes(Current_Node).Count Then .Bone_4 = Nodes(Current_Node)(Data(Offset + 23))

                                .Weight_1 = Data(Offset + 24) / 100.0F
                                .Weight_2 = Data(Offset + 25) / 100.0F
                                .Weight_3 = Data(Offset + 26) / 100.0F
                                .Weight_4 = Data(Offset + 27) / 100.0F
                            Case &H24
                                Select Case Vertex_Bytes And &HFFFF
                                    Case &H8E82, &HAA83, &HAADB, &HAE83
                                        .Bone_1 = Nodes(Current_Node)(0)

                                        .Weight_1 = 1
                                    Case &H8F03, &HAF03, &HEE03, &HEE83, &HEED3, &HEF03, &HEF53, &HEF83, &HEFD3
                                        .Bone_1 = Nodes(Current_Node)(0)
                                        .Bone_2 = Nodes(Current_Node)(1)
                                        .Bone_3 = Nodes(Current_Node)(2)
                                        .Bone_4 = Nodes(Current_Node)(3)

                                        .Weight_1 = Data(Offset + 32) / 100.0F
                                        .Weight_2 = Data(Offset + 33) / 100.0F
                                        .Weight_3 = Data(Offset + 34) / 100.0F
                                        .Weight_4 = Data(Offset + 35) / 100.0F
                                    Case &HEEDB
                                        If Data(Offset + 32) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 32))
                                        If Data(Offset + 33) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 33))

                                        .Weight_1 = Data(Offset + 34) / 100.0F
                                        .Weight_2 = Data(Offset + 35) / 100.0F
                                End Select
                            Case &H28
                                If Nodes(Current_Node).Count < 5 Then
                                    .Bone_1 = Nodes(Current_Node)(0)
                                    .Bone_2 = Nodes(Current_Node)(1)
                                    .Bone_3 = Nodes(Current_Node)(2)
                                    .Bone_4 = Nodes(Current_Node)(3)

                                    .Weight_1 = Data(Offset + 36) / 100.0F
                                    .Weight_2 = Data(Offset + 37) / 100.0F
                                    .Weight_3 = Data(Offset + 38) / 100.0F
                                    .Weight_4 = Data(Offset + 39) / 100.0F
                                Else
                                    If Data(Offset + 32) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 32))
                                    If Data(Offset + 33) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 33))
                                    If Data(Offset + 34) < Nodes(Current_Node).Count Then .Bone_3 = Nodes(Current_Node)(Data(Offset + 34))

                                    .Weight_1 = Data(Offset + 35) / 100.0F
                                    .Weight_2 = Data(Offset + 36) / 100.0F
                                    .Weight_3 = Data(Offset + 37) / 100.0F
                                End If
                            Case &H2C
                                Select Case Vertex_Bytes And &HFFFF
                                    Case &HAA83, &HAE83
                                        If Data(Offset + 36) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 36))
                                        If Data(Offset + 37) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 37))
                                        If Data(Offset + 38) < Nodes(Current_Node).Count Then .Bone_3 = Nodes(Current_Node)(Data(Offset + 38))

                                        .Weight_1 = Data(Offset + 39) / 100.0F
                                        .Weight_2 = Data(Offset + 40) / 100.0F
                                        .Weight_3 = Data(Offset + 41) / 100.0F
                                    Case &HAB83, &HAF83, &HEF83, &HEFD3
                                        If Data(Offset + 36) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 36))
                                        If Data(Offset + 37) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 37))
                                        If Data(Offset + 38) < Nodes(Current_Node).Count Then .Bone_3 = Nodes(Current_Node)(Data(Offset + 38))
                                        If Data(Offset + 39) < Nodes(Current_Node).Count Then .Bone_4 = Nodes(Current_Node)(Data(Offset + 39))

                                        .Weight_1 = Data(Offset + 40) / 100.0F
                                        .Weight_2 = Data(Offset + 41) / 100.0F
                                        .Weight_3 = Data(Offset + 42) / 100.0F
                                        .Weight_4 = Data(Offset + 43) / 100.0F
                                    Case Else
                                        .Bone_1 = Nodes(Current_Node)(0)

                                        .Weight_1 = 1
                                End Select
                            Case &H30
                                If Nodes(Current_Node).Count = 1 Then
                                    .Bone_1 = Nodes(Current_Node)(0)

                                    .Weight_1 = 1
                                Else
                                    .Bone_1 = Nodes(Current_Node)(0)
                                    .Bone_2 = Nodes(Current_Node)(1)
                                    .Bone_3 = Nodes(Current_Node)(2)
                                    .Bone_4 = Nodes(Current_Node)(3)

                                    .Weight_1 = Data(Offset + 44) / 100.0F
                                    .Weight_2 = Data(Offset + 45) / 100.0F
                                    .Weight_3 = Data(Offset + 46) / 100.0F
                                    .Weight_4 = Data(Offset + 47) / 100.0F
                                End If
                            Case &H34
                                If Nodes(Current_Node).Count < 5 Then
                                    .Bone_1 = Nodes(Current_Node)(0)
                                    .Bone_2 = Nodes(Current_Node)(1)
                                    .Bone_3 = Nodes(Current_Node)(2)
                                    .Bone_4 = Nodes(Current_Node)(3)

                                    .Weight_1 = Data(Offset + 48) / 100.0F
                                    .Weight_2 = Data(Offset + 49) / 100.0F
                                    .Weight_3 = Data(Offset + 50) / 100.0F
                                    .Weight_4 = Data(Offset + 51) / 100.0F
                                Else
                                    If Data(Offset + 44) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 44))
                                    If Data(Offset + 45) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 45))
                                    If Data(Offset + 46) < Nodes(Current_Node).Count Then .Bone_3 = Nodes(Current_Node)(Data(Offset + 46))
                                    If Data(Offset + 47) < Nodes(Current_Node).Count Then .Bone_4 = Nodes(Current_Node)(Data(Offset + 47))

                                    .Weight_1 = Data(Offset + 48) / 100.0F
                                    .Weight_2 = Data(Offset + 49) / 100.0F
                                    .Weight_3 = Data(Offset + 50) / 100.0F
                                    .Weight_4 = Data(Offset + 51) / 100.0F
                                End If
                            Case &H38
                                Select Case Vertex_Bytes And &HFFFF
                                    Case &HEA83, &HEAD3, &HEE83, &HEED3, &HEEDB, &HEEE3, &HEEF3
                                        If Data(Offset + 48) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 48))
                                        If Data(Offset + 49) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 49))
                                        If Data(Offset + 50) < Nodes(Current_Node).Count Then .Bone_3 = Nodes(Current_Node)(Data(Offset + 50))

                                        .Weight_1 = Data(Offset + 51) / 100.0F
                                        .Weight_2 = Data(Offset + 52) / 100.0F
                                        .Weight_3 = Data(Offset + 53) / 100.0F
                                    Case &HEB83, &HEBD3, &HEF83, &HEFD3, &HEFDB, &HEFE3, &HEFF3, &HEFFB
                                        If Data(Offset + 48) < Nodes(Current_Node).Count Then .Bone_1 = Nodes(Current_Node)(Data(Offset + 48))
                                        If Data(Offset + 49) < Nodes(Current_Node).Count Then .Bone_2 = Nodes(Current_Node)(Data(Offset + 49))
                                        If Data(Offset + 50) < Nodes(Current_Node).Count Then .Bone_3 = Nodes(Current_Node)(Data(Offset + 50))
                                        If Data(Offset + 51) < Nodes(Current_Node).Count Then .Bone_4 = Nodes(Current_Node)(Data(Offset + 51))

                                        .Weight_1 = Data(Offset + 52) / 100.0F
                                        .Weight_2 = Data(Offset + 53) / 100.0F
                                        .Weight_3 = Data(Offset + 54) / 100.0F
                                        .Weight_4 = Data(Offset + 55) / 100.0F
                                End Select
                        End Select
                    End If
                End With

                Vertex_Count += 1
                Offset += Vertex_Data_Format
            Next

            Parse_Faces(Data, Entry, Data_Offset, Face_Offset, Count)
            If Face_Offset <> Face_Offset_2 Then Parse_Faces(Data, Entry, Data_Offset, Face_Offset_2, Count)
            If Face_Offset_3 > 0 Then Parse_Faces(Data, Entry, Data_Offset, Face_Offset_3, Count)

            Model_Object(Entry).Texture_ID = Texture_ID
            Model_Object(Entry).Vertex_Format = Vertex_Data_Format
        Next

        Dim Name_Table_Base_Pointer As Integer
        Dim Name_Table_Length As Integer
        If Version = BCH_Version.XY Then
            If Model_Type = ModelType.Character Then
                Name_Table_Base_Pointer = 8
            ElseIf Model_Type = ModelType.Map Then
                Name_Table_Base_Pointer = &H14
            End If
            Name_Table_Length = &H58
        ElseIf Version = BCH_Version.ORAS Then
            Name_Table_Base_Pointer = &H18
            Name_Table_Length = &H2C
        End If

        ReDim Model_Texture_Index(Texture_Entries - 1)
        ReDim Model_Bump_Map_Index(Texture_Entries - 1)
        For Index As Integer = 0 To Texture_Entries - 1
            Dim Texture_Offset As Integer = Texture_Names_Offset + Read32(Data, (Texture_Table_Offset + Index * Name_Table_Length) + Name_Table_Base_Pointer)
            Dim Normal_Offset As Integer = Texture_Names_Offset + Read32(Data, (Texture_Table_Offset + Index * Name_Table_Length) + Name_Table_Base_Pointer + 8)

            'Textura
            Dim Texture_Name As String = Nothing
            Do
                Dim Value As Integer = Data(Texture_Offset)
                Texture_Offset += 1
                If Value <> 0 Then Texture_Name &= Chr(Value) Else Exit Do
            Loop
            Model_Texture_Index(Index) = Texture_Name

            If Model_Type = ModelType.Character Then
                'Mapa de Normals/Bump Map
                Dim Normal_Name As String = Nothing
                Do
                    Dim Value As Integer = Data(Normal_Offset)
                    Normal_Offset += 1
                    If Value <> 0 Then Normal_Name &= Chr(Value) Else Exit Do
                Loop
                Model_Bump_Map_Index(Index) = Normal_Name
            End If
        Next

        ReDim Model_Bone(Bone_Entries - 1)
        Bones_Offset += (Bone_Entries * &HC) + &HC
        For Bone As Integer = 0 To Bone_Entries - 1
            Dim Bone_Parent As Integer = Signed_Short(Read16(Data, Bones_Offset + 4))
            Dim Bone_Name_Offset As Integer = Read32(Data, Bones_Offset + 92)

            Dim MyBone As OhanaBone
            With MyBone
                .Name = Nothing
                Do
                    Dim Value As Integer = Data(Texture_Names_Offset + Bone_Name_Offset)
                    Bone_Name_Offset += 1
                    If Value <> 0 Then .Name &= Chr(Value) Else Exit Do
                Loop
                .Parent_ID = Bone_Parent

                .Translation.X = BitConverter.ToSingle(Data, Bones_Offset + 32) / Scale
                .Translation.Y = BitConverter.ToSingle(Data, Bones_Offset + 36) / Scale
                .Translation.Z = BitConverter.ToSingle(Data, Bones_Offset + 40) / Scale

                .Rotation.X = BitConverter.ToSingle(Data, Bones_Offset + 20)
                .Rotation.Y = BitConverter.ToSingle(Data, Bones_Offset + 24)
                .Rotation.Z = BitConverter.ToSingle(Data, Bones_Offset + 28)

                .Scale.X = BitConverter.ToSingle(Data, Bones_Offset + 8)
                .Scale.Y = BitConverter.ToSingle(Data, Bones_Offset + 12)
                .Scale.Z = BitConverter.ToSingle(Data, Bones_Offset + 16)
            End With
            Model_Bone(Bone) = MyBone

            Dim Bone_Mtx As New Matrix

            Bone_Mtx.M11 = BitConverter.ToSingle(Data, Bones_Offset + 44)
            Bone_Mtx.M12 = BitConverter.ToSingle(Data, Bones_Offset + 48)
            Bone_Mtx.M13 = BitConverter.ToSingle(Data, Bones_Offset + 52)
            Bone_Mtx.M14 = BitConverter.ToSingle(Data, Bones_Offset + 56)

            Bone_Mtx.M21 = BitConverter.ToSingle(Data, Bones_Offset + 60)
            Bone_Mtx.M22 = BitConverter.ToSingle(Data, Bones_Offset + 64)
            Bone_Mtx.M23 = BitConverter.ToSingle(Data, Bones_Offset + 68)
            Bone_Mtx.M24 = BitConverter.ToSingle(Data, Bones_Offset + 72)

            Bone_Mtx.M31 = BitConverter.ToSingle(Data, Bones_Offset + 76)
            Bone_Mtx.M32 = BitConverter.ToSingle(Data, Bones_Offset + 80)
            Bone_Mtx.M33 = BitConverter.ToSingle(Data, Bones_Offset + 84)
            Bone_Mtx.M34 = BitConverter.ToSingle(Data, Bones_Offset + 88)

            Bones_Offset += 100
        Next

        Dim TempLst As New List(Of String)
        For Index As Integer = 0 To Model_Texture_Index.Length - 1
            If Not TempLst.Contains(Model_Texture_Index(Index)) Then TempLst.Add(Model_Texture_Index(Index))
        Next
        With Info
            .Vertex_Count = Vertex_Count
            .Triangles_Count = Total_Vertex \ 3
            .Bones_Count = Bone_Entries
            .Textures_Count = Texture_ID_List.Count - (Texture_Entries - TempLst.Count)
        End With
    End Sub
    Public Sub Load_Textures(File_Name As String, Optional Version As BCH_Version = BCH_Version.XY, Optional Create_DX_Texture As Boolean = True)
        Dim Data() As Byte = File.ReadAllBytes(File_Name)
        Dim File_Magic As String = Nothing
        For i As Integer = 0 To 1
            File_Magic &= Chr(Data(i))
        Next
        Dim BCH_Table_Offset As Integer = If(File_Magic = "PT", 4, 8)
        Dim BCH_Offset As Integer = Read32(Data, BCH_Table_Offset)

        Dim Texture_Description_Length As Integer
        Dim Desc_Texture_Pointer As Integer
        Dim Desc_Texture_Format As Integer
        If Version = BCH_Version.XY Then
            Texture_Description_Length = &H20
            Desc_Texture_Pointer = 8
            Desc_Texture_Format = &H10
        ElseIf Version = BCH_Version.ORAS Then
            Texture_Description_Length = &H30
            Desc_Texture_Pointer = &H10
            Desc_Texture_Format = &H18
        End If

        Model_Texture = New List(Of OhanaTexture)
        While BCH_Offset > 0
            If BCH_Offset = Data.Length Then Exit While
            Dim Magic As String = Nothing
            For i As Integer = 0 To 2
                Magic &= Chr(Data(BCH_Offset + i))
            Next

            If Magic = "BCH" Then
                Dim Texture_Names_Offset As Integer = BCH_Offset + Read32(Data, BCH_Offset + &HC)
                Dim Description_Offset As Integer = BCH_Offset + Read32(Data, BCH_Offset + &H10)
                Dim Data_Offset As Integer = BCH_Offset + Read32(Data, BCH_Offset + &H14)
                Dim Texture_Count As Integer
                If Version = BCH_Version.XY Then
                    Texture_Count = Read32(Data, BCH_Offset + &H60)
                ElseIf Version = BCH_Version.ORAS Then
                    Texture_Count = Read32(Data, BCH_Offset + &H6C)
                End If

                Dim Texture_Names(Texture_Count - 1) As String
                Dim Tmp As Integer = 0
                For i As Integer = 0 To Texture_Count - 1
                    Dim Str As String = Nothing
                    Do
                        Dim Value As Integer = Data(Texture_Names_Offset + Tmp)
                        Tmp += 1
                        If Value <> 0 Then Str &= Chr(Value) Else Exit Do
                    Loop
                    Texture_Names(i) = Str
                Next

                Dim Index As Integer = 0
                While Index < Texture_Count
                    If Read32(Data, Description_Offset + Desc_Texture_Pointer - Texture_Description_Length) <> Read32(Data, Description_Offset + Desc_Texture_Pointer) Then
                        Dim Width As Integer = Read16(Data, Description_Offset + 2)
                        Dim Height As Integer = Read16(Data, Description_Offset)

                        Dim Format As Integer = Data(Description_Offset + Desc_Texture_Format)
                        Dim Texture_Data_Offset As Integer = Data_Offset + Read32(Data, Description_Offset + Desc_Texture_Pointer)

                        Dim Out((Width * Height * 4) - 1) As Byte
                        Dim Offset As Integer = Texture_Data_Offset
                        Dim Low_High_Toggle As Boolean = False

                        If Format = 12 Or Format = 13 Then 'ETC1 (iPACKMAN)
                            Dim Temp_Buffer(((Width * Height) \ 2) - 1) As Byte
                            Dim Alphas(Temp_Buffer.Length - 1) As Byte
                            If Format = 12 Then
                                Buffer.BlockCopy(Data, Offset, Temp_Buffer, 0, Temp_Buffer.Length)
                                For j As Integer = 0 To Alphas.Length - 1
                                    Alphas(j) = &HFF
                                Next
                            Else
                                Dim k As Integer = 0
                                For j As Integer = 0 To (Width * Height) - 1
                                    Buffer.BlockCopy(Data, Offset + j + 8, Temp_Buffer, k, 8)
                                    Buffer.BlockCopy(Data, Offset + j, Alphas, k, 8)
                                    k += 8
                                    j += 15
                                Next
                            End If
                            Dim Temp_2() As Byte = ETC1_Decompress(Temp_Buffer, Alphas, Width, Height)

                            'Os tiles com compressão ETC1 no 3DS estão embaralhados
                            Dim Tile_Scramble(((Width \ 4) * (Height \ 4)) - 1) As Integer
                            Dim Base_Accumulator As Integer = 0, Line_Accumulator As Integer = 0
                            Dim Base_Number As Integer = 0, Line_Number As Integer = 0

                            For Tile As Integer = 0 To Tile_Scramble.Length - 1
                                If (Tile Mod (Width \ 4) = 0) And Tile > 0 Then
                                    If Line_Accumulator < 1 Then
                                        Line_Accumulator += 1
                                        Line_Number += 2
                                        Base_Number = Line_Number
                                    Else
                                        Line_Accumulator = 0
                                        Base_Number -= 2
                                        Line_Number = Base_Number
                                    End If
                                End If

                                Tile_Scramble(Tile) = Base_Number

                                If Base_Accumulator < 1 Then
                                    Base_Accumulator += 1
                                    Base_Number += 1
                                Else
                                    Base_Accumulator = 0
                                    Base_Number += 3
                                End If
                            Next

                            Dim i As Integer = 0
                            For Tile_Y As Integer = 0 To (Height \ 4) - 1
                                For Tile_X As Integer = 0 To (Width \ 4) - 1
                                    Dim TX As Integer = Tile_Scramble(i) Mod (Width \ 4)
                                    Dim TY As Integer = (Tile_Scramble(i) - TX) \ (Width \ 4)
                                    For Y As Integer = 0 To 3
                                        For X As Integer = 0 To 3
                                            Dim Out_Offset As Integer = ((Tile_X * 4) + X + (((Height - 1) - ((Tile_Y * 4) + Y)) * Width)) * 4
                                            Dim Image_Offset As Integer = ((TX * 4) + X + (((TY * 4) + Y) * Width)) * 4

                                            Out(Out_Offset) = Temp_2(Image_Offset)
                                            Out(Out_Offset + 1) = Temp_2(Image_Offset + 1)
                                            Out(Out_Offset + 2) = Temp_2(Image_Offset + 2)
                                            Out(Out_Offset + 3) = Temp_2(Image_Offset + 3)
                                        Next
                                    Next
                                    i += 1
                                Next
                            Next
                        Else
                            For Tile_Y As Integer = 0 To (Height \ 8) - 1
                                For Tile_X As Integer = 0 To (Width \ 8) - 1
                                    For i As Integer = 0 To 63
                                        Dim X As Integer = Tile_Order(i) Mod 8
                                        Dim Y As Integer = (Tile_Order(i) - X) \ 8
                                        Dim Out_Offset As Integer = ((Tile_X * 8) + X + (((Height - 1) - ((Tile_Y * 8) + Y)) * Width)) * 4
                                        Select Case Format
                                            Case 0 'R8G8B8A8
                                                Buffer.BlockCopy(Data, Offset + 1, Out, Out_Offset, 3)
                                                Out(Out_Offset + 3) = Data(Offset)
                                                Offset += 4
                                            Case 1 'R8G8B8 (sem transparência)
                                                Buffer.BlockCopy(Data, Offset, Out, Out_Offset, 3)
                                                Out(Out_Offset + 3) = &HFF
                                                Offset += 3
                                            Case 2 'R5G5B5A1
                                                Dim Pixel_Data As Integer = Read16(Data, Offset)
                                                Out(Out_Offset) = Convert.ToByte(((Pixel_Data >> 11) And &H1F) * 8)
                                                Out(Out_Offset + 1) = Convert.ToByte(((Pixel_Data >> 6) And &H1F) * 8)
                                                Out(Out_Offset + 2) = Convert.ToByte(((Pixel_Data >> 1) And &H1F) * 8)
                                                Out(Out_Offset + 3) = Convert.ToByte((Pixel_Data And 1) * &HFF)
                                                Offset += 2
                                            Case 3 'R5G6B5
                                                Dim Pixel_Data As Integer = Read16(Data, Offset)
                                                Out(Out_Offset) = Convert.ToByte(((Pixel_Data >> 11) And &H1F) * 8)
                                                Out(Out_Offset + 1) = Convert.ToByte(((Pixel_Data >> 5) And &H3F) * 4)
                                                Out(Out_Offset + 2) = Convert.ToByte(((Pixel_Data) And &H1F) * 8)
                                                Out(Out_Offset + 3) = &HFF
                                                Offset += 2
                                            Case 4 'R4G4B4A4
                                                Dim Pixel_Data As Integer = Read16(Data, Offset)
                                                Out(Out_Offset) = Convert.ToByte(((Pixel_Data >> 12) And &HF) * &H11)
                                                Out(Out_Offset + 1) = Convert.ToByte(((Pixel_Data >> 8) And &HF) * &H11)
                                                Out(Out_Offset + 2) = Convert.ToByte(((Pixel_Data >> 4) And &HF) * &H11)
                                                Out(Out_Offset + 3) = Convert.ToByte((Pixel_Data And &HF) * &H11)
                                                Offset += 2
                                            Case 5 'L8A8
                                                Dim Pixel_Data As Byte = Data(Offset + 1)
                                                Out(Out_Offset) = Pixel_Data
                                                Out(Out_Offset + 1) = Pixel_Data
                                                Out(Out_Offset + 2) = Pixel_Data
                                                Out(Out_Offset + 3) = Data(Offset)
                                                Offset += 2
                                            Case 6 'HILO8
                                            Case 7 'L8
                                                Out(Out_Offset) = Data(Offset)
                                                Out(Out_Offset + 1) = Data(Offset)
                                                Out(Out_Offset + 2) = Data(Offset)
                                                Out(Out_Offset + 3) = &HFF
                                                Offset += 1
                                            Case 8 'A8
                                                Out(Out_Offset) = &HFF
                                                Out(Out_Offset + 1) = &HFF
                                                Out(Out_Offset + 2) = &HFF
                                                Out(Out_Offset + 3) = Data(Offset)
                                                Offset += 1
                                            Case 9 'L4A4
                                                Dim Luma As Integer = Data(Offset) And &HF
                                                Dim Alpha As Integer = (Data(Offset) And &HF0) >> 4
                                                Out(Out_Offset) = Convert.ToByte(Luma * &H11)
                                                Out(Out_Offset + 1) = Convert.ToByte(Luma * &H11)
                                                Out(Out_Offset + 2) = Convert.ToByte(Luma * &H11)
                                                Out(Out_Offset + 3) = Convert.ToByte(Alpha * &H11)
                                            Case 10 'L4
                                                Dim Pixel_Data As Integer
                                                If Low_High_Toggle Then
                                                    Pixel_Data = Data(Offset) And &HF
                                                    Offset += 1
                                                Else
                                                    Pixel_Data = (Data(Offset) And &HF0) >> 4
                                                End If
                                                Out(Out_Offset) = Convert.ToByte(Pixel_Data * &H11)
                                                Out(Out_Offset + 1) = Convert.ToByte(Pixel_Data * &H11)
                                                Out(Out_Offset + 2) = Convert.ToByte(Pixel_Data * &H11)
                                                Out(Out_Offset + 3) = &HFF
                                                Low_High_Toggle = Not Low_High_Toggle
                                        End Select
                                    Next
                                Next
                            Next
                        End If

                        Dim MyTex As New OhanaTexture
                        Dim Img As New Bitmap(Width, Height, Imaging.PixelFormat.Format32bppArgb)
                        Dim ImgData As BitmapData = Img.LockBits(New Rectangle(0, 0, Img.Width, Img.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
                        Marshal.Copy(Out, 0, ImgData.Scan0, Out.Length)
                        Img.UnlockBits(ImgData)
                        MyTex.Image = Img
                        MyTex.Image.RotateFlip(RotateFlipType.RotateNoneFlipY)

                        If File_Magic = "PT" Then
                            Out = Mirror_Texture(Out, Width, Height)
                            Width *= 2
                        End If

                        Dim Texture As Texture = Nothing
                        If Create_DX_Texture Then
                            Texture = New Texture(Device, Width, Height, 1, Usage.None, Direct3D.Format.A8R8G8B8, Pool.Managed)
                            Dim pData As GraphicsStream = Texture.LockRectangle(0, LockFlags.None)
                            pData.Write(Out, 0, Out.Length)
                            Texture.UnlockRectangle(0)
                        End If

                        With MyTex
                            If Create_DX_Texture Then .Texture = Texture
                            .Name = Texture_Names(Index)
                            Select Case Format
                                Case 0, 2, 4, 5, 13
                                    .Has_Alpha = True
                            End Select

                            .Format = Format
                        End With
                        Model_Texture.Add(MyTex)

                        Index += 1
                    End If

                    Description_Offset += Texture_Description_Length
                End While
            End If

            If File_Magic = "PT" Then
                If BCH_Table_Offset < 8 Then BCH_Table_Offset += 4 Else Exit While
            Else
                BCH_Table_Offset += &H10
            End If

            BCH_Offset = Read32(Data, BCH_Table_Offset)
        End While
    End Sub
    Public Sub Export_SMD(File_Name As String)
        Dim Info As New NumberFormatInfo
        Info.NumberDecimalSeparator = "."
        Info.NumberDecimalDigits = 6

        Dim Out As New StringBuilder
        Out.AppendLine("version 1")
        Out.AppendLine("nodes")
        Dim Node_Index As Integer
        For Each Bone As OhanaBone In Model_Bone
            With Bone
                Out.AppendLine(Node_Index & " """ & Bone.Name & """ " & Bone.Parent_ID)
                Node_Index += 1
            End With
        Next
        Out.AppendLine("end")
        Out.AppendLine("skeleton")
        Out.AppendLine("time 0")
        Dim Bone_Index As Integer
        For Each Bone As OhanaBone In Model_Bone
            With Bone
                Out.AppendLine(Bone_Index & _
                               " " & .Translation.X.ToString("N", Info) & " " & .Translation.Y.ToString("N", Info) & " " & .Translation.Z.ToString("N", Info) & _
                               " " & .Rotation.X.ToString("N", Info) & " " & .Rotation.Y.ToString("N", Info) & " " & .Rotation.Z.ToString("N", Info))
                Bone_Index += 1
            End With
        Next
        Out.AppendLine("end")
        Out.AppendLine("triangles")
        Dim Temp_Count As Integer
        For Each Model As VertexList In Model_Object
            With Model
                For Each Index As Integer In .Index
                    If Temp_Count = 0 Then Out.AppendLine(Model_Texture_Index(.Texture_ID) & ".png")
                    Dim CurrVert As OhanaVertex
                    If Index < .Vertice.Length Then CurrVert = .Vertice(Index)

                    Dim Bone_Info As String = Nothing
                    Dim Links As Integer = 0
                    If CurrVert.Weight_1 > 0 Then
                        Links = 1
                        Bone_Info = " " & CurrVert.Bone_1 & " " & CurrVert.Weight_1.ToString(Info)
                    End If
                    If CurrVert.Weight_2 > 0 Then
                        Links += 1
                        Bone_Info &= " " & CurrVert.Bone_2 & " " & CurrVert.Weight_2.ToString(Info)
                    End If
                    If CurrVert.Weight_3 > 0 Then
                        Links += 1
                        Bone_Info &= " " & CurrVert.Bone_3 & " " & CurrVert.Weight_3.ToString(Info)
                    End If
                    If CurrVert.Weight_4 > 0 Then
                        Links += 1
                        Bone_Info &= " " & CurrVert.Bone_4 & " " & CurrVert.Weight_4.ToString(Info)
                    End If
                    Bone_Info = Links & Bone_Info

                    Out.AppendLine(Model_Bone.Length & " " & _
                                   CurrVert.X.ToString(Info) & " " & CurrVert.Y.ToString(Info) & " " & CurrVert.Z.ToString(Info) & " " & _
                                   CurrVert.NX.ToString(Info) & " " & CurrVert.NY.ToString(Info) & " " & CurrVert.NZ.ToString(Info) & " " & _
                                   CurrVert.U.ToString(Info) & " " & CurrVert.V.ToString(Info) & " " & Bone_Info)
                    If Temp_Count < 2 Then Temp_Count += 1 Else Temp_Count = 0
                Next
            End With
        Next
        Out.AppendLine("end")

        File.WriteAllText(File_Name, Out.ToString)
    End Sub
    Private Function Mirror_Texture(Data() As Byte, Width As Integer, Height As Integer) As Byte()
        Dim Out(((Width * 2) * Height * 4) - 1) As Byte
        For Y As Integer = 0 To Height - 1
            For X As Integer = 0 To Width - 1
                Dim Offset As Integer = (X + (Y * Width)) * 4
                Dim Offset_2 As Integer = (X + (Y * (Width * 2))) * 4
                Dim Offset_3 As Integer = ((Width + (Width - X - 1)) + (Y * (Width * 2))) * 4
                Buffer.BlockCopy(Data, Offset, Out, Offset_2, 4)
                Buffer.BlockCopy(Data, Offset, Out, Offset_3, 4)
            Next
        Next
        Return Out
    End Function
    Private Function ETC1_Decompress(Data() As Byte, Alphas() As Byte, Width As Integer, Height As Integer) As Byte()
        Dim Out((Width * Height * 4) - 1) As Byte
        Dim Offset As Integer
        For Y As Integer = 0 To (Height \ 4) - 1
            For X As Integer = 0 To (Width \ 4) - 1
                Dim Block(7) As Byte
                Dim Alphas_Block(7) As Byte
                For i As Integer = 0 To 7
                    Block(7 - i) = Data(Offset + i)
                    Alphas_Block(i) = Alphas(Offset + i)
                Next
                Offset += 8
                Block = ETC1_Decompress_Block(Block)

                Dim Low_High_Toggle As Boolean = False
                Dim Alpha_Offset As Integer = 0
                For TX As Integer = 0 To 3
                    For TY As Integer = 0 To 3
                        Dim Out_Offset As Integer = (X * 4 + TX + ((Y * 4 + TY) * Width)) * 4
                        Dim Block_Offset As Integer = (TX + (TY * 4)) * 4
                        Buffer.BlockCopy(Block, Block_Offset, Out, Out_Offset, 3)

                        Dim Alpha_Data As Integer
                        If Low_High_Toggle Then
                            Alpha_Data = (Alphas_Block(Alpha_Offset) And &HF0) >> 4
                            Alpha_Offset += 1
                        Else
                            Alpha_Data = Alphas_Block(Alpha_Offset) And &HF
                        End If
                        Low_High_Toggle = Not Low_High_Toggle
                        Out(Out_Offset + 3) = Alpha_Data * &H11
                    Next
                Next
            Next
        Next
        Return Out
    End Function
    Private Function ETC1_Decompress_Block(Data() As Byte) As Byte()
        'Ericsson Texture Compression
        Dim Block_Top As Integer = Read32(Data, 0)
        Dim Block_Bottom As Integer = Read32(Data, 4)

        Dim Flip As Boolean = Block_Top And &H1000000
        Dim Difference As Boolean = Block_Top And &H2000000

        Dim R1, G1, B1, R2, G2, B2 As Integer
        Dim R, G, B As Integer

        If Difference Then
            R1 = Block_Top And &HF8
            G1 = (Block_Top And &HF800) >> 8
            B1 = (Block_Top And &HF80000) >> 16

            R = Signed_Byte(R1 >> 3) + (Signed_Byte((Block_Top And 7) << 5) >> 5)
            G = Signed_Byte(G1 >> 3) + (Signed_Byte((Block_Top And &H700) >> 3) >> 5)
            B = Signed_Byte(B1 >> 3) + (Signed_Byte((Block_Top And &H70000) >> 11) >> 5)

            R2 = R
            G2 = G
            B2 = B

            R1 = R1 + (R1 >> 5)
            G1 = G1 + (G1 >> 5)
            B1 = B1 + (B1 >> 5)

            R2 = (R2 << 3) + (R2 >> 2)
            G2 = (G2 << 3) + (G2 >> 2)
            B2 = (B2 << 3) + (B2 >> 2)
        Else
            R1 = Block_Top And &HF0
            R1 = R1 + (R1 >> 4)
            G1 = (Block_Top And &HF000) >> 8
            G1 = G1 + (G1 >> 4)
            B1 = (Block_Top And &HF00000) >> 16
            B1 = B1 + (B1 >> 4)

            R2 = (Block_Top And &HF) << 4
            R2 = R2 + (R2 >> 4)
            G2 = (Block_Top And &HF00) >> 4
            G2 = G2 + (G2 >> 4)
            B2 = (Block_Top And &HF0000) >> 12
            B2 = B2 + (B2 >> 4)
        End If

        Dim Mod_Table_1 As Integer = (Block_Top >> 29) And 7
        Dim Mod_Table_2 As Integer = (Block_Top >> 26) And 7

        Dim Out((4 * 4 * 4) - 1) As Byte
        If Flip = False Then
            For Y As Integer = 0 To 3
                For X As Integer = 0 To 1
                    Dim Col_1 As Color = Modify_Pixel(R1, G1, B1, X, Y, Block_Bottom, Mod_Table_1)
                    Dim Col_2 As Color = Modify_Pixel(R2, G2, B2, X + 2, Y, Block_Bottom, Mod_Table_2)
                    Out((Y * 4 + X) * 4) = Col_1.R
                    Out(((Y * 4 + X) * 4) + 1) = Col_1.G
                    Out(((Y * 4 + X) * 4) + 2) = Col_1.B
                    Out((Y * 4 + X + 2) * 4) = Col_2.R
                    Out(((Y * 4 + X + 2) * 4) + 1) = Col_2.G
                    Out(((Y * 4 + X + 2) * 4) + 2) = Col_2.B
                Next
            Next
        Else
            For Y As Integer = 0 To 1
                For X As Integer = 0 To 3
                    Dim Col_1 As Color = Modify_Pixel(R1, G1, B1, X, Y, Block_Bottom, Mod_Table_1)
                    Dim Col_2 As Color = Modify_Pixel(R2, G2, B2, X, Y + 2, Block_Bottom, Mod_Table_2)
                    Out((Y * 4 + X) * 4) = Col_1.R
                    Out(((Y * 4 + X) * 4) + 1) = Col_1.G
                    Out(((Y * 4 + X) * 4) + 2) = Col_1.B
                    Out(((Y + 2) * 4 + X) * 4) = Col_2.R
                    Out((((Y + 2) * 4 + X) * 4) + 1) = Col_2.G
                    Out((((Y + 2) * 4 + X) * 4) + 2) = Col_2.B
                Next
            Next
        End If

        Return Out
    End Function
    Private Function Modify_Pixel(R As Integer, G As Integer, B As Integer, X As Integer, Y As Integer, Mod_Block As Integer, Mod_Table As Integer) As Color
        Dim Index As Integer = X * 4 + Y
        Dim Pixel_Modulation As Integer
        Dim MSB As Integer = Mod_Block << 1
        If Index < 8 Then
            Pixel_Modulation = Modulation_Table(Mod_Table, ((Mod_Block >> (Index + 24)) And 1) + ((MSB >> (Index + 8)) And 2))
        Else
            Pixel_Modulation = Modulation_Table(Mod_Table, ((Mod_Block >> (Index + 8)) And 1) + ((MSB >> (Index - 8)) And 2))
        End If

        R = Clip(R + Pixel_Modulation)
        G = Clip(G + Pixel_Modulation)
        B = Clip(B + Pixel_Modulation)

        Return Color.FromArgb(B, G, R)
    End Function
    Private Function Clip(Value As Integer) As Byte
        If Value > &HFF Then
            Return &HFF
        ElseIf Value < 0 Then
            Return 0
        Else
            Return Value And &HFF
        End If
    End Function
    Private Function Signed_Byte(Byte_To_Convert As Byte) As SByte
        If (Byte_To_Convert < &H80) Then Return Byte_To_Convert
        Return Byte_To_Convert - &H100
    End Function
    Private Function Signed_Short(Short_To_Convert As Integer) As Integer
        If (Short_To_Convert < &H8000) Then Return Short_To_Convert
        Return Short_To_Convert - &H10000
    End Function
    Private Sub Parse_Faces(Data() As Byte, Entry As Integer, Data_Offset As Integer, Face_Offset As Integer, Fmt As Integer)
        Dim Face_Data_Format As Integer = 1
        If Fmt > &HFF Then Face_Data_Format = 2
        With Model_Object(Entry)
            Dim Face_Data_Offset As Integer = Data_Offset + Read32(Data, Face_Offset + &H10)
            Dim Face_Data_Length As Integer = Read32(Data, Face_Offset + &H18)
            Dim Old_Length As Integer
            If .Index IsNot Nothing Then Old_Length = .Index.Length
            ReDim Preserve .Index(Old_Length + Face_Data_Length - 1)
            Dim Offset As Integer = Face_Data_Offset
            For Index As Integer = 0 To Face_Data_Length - 1 Step 3
                If Face_Data_Format = 2 Then
                    .Index(Old_Length + Index) = Convert.ToInt32(Read16(Data, Offset))
                    .Index(Old_Length + Index + 1) = Convert.ToInt32(Read16(Data, Offset + 2))
                    .Index(Old_Length + Index + 2) = Convert.ToInt32(Read16(Data, Offset + 4))
                Else
                    .Index(Old_Length + Index) = Data(Offset)
                    .Index(Old_Length + Index + 1) = Data(Offset + 1)
                    .Index(Old_Length + Index + 2) = Data(Offset + 2)
                End If
                Offset += 3 * Face_Data_Format
                Total_Vertex += 3
            Next
        End With
    End Sub
    Public Sub Render()
        'Define a posição da "câmera"
        Device.Transform.Projection = Matrix.PerspectiveFovLH(Math.PI / 4, CSng(640 / 480), 0.1F, 200.0F)
        Device.Transform.View = Matrix.LookAtLH(New Vector3(0.0F, 0.0F, 20.0F), New Vector3(0.0F, 0.0F, 0.0F), New Vector3(0.0F, 1.0F, 0.0F))

        Do
            If Model_Object IsNot Nothing Then
                Device.Clear(ClearFlags.Target, Color.Black, 1.0F, 0)
                Device.Clear(ClearFlags.ZBuffer, Color.Black, 1.0F, 0)
                Device.BeginScene()

                Dim MyMaterial As New Material
                MyMaterial.Diffuse = Color.White
                MyMaterial.Ambient = Color.White
                Device.Material = MyMaterial

                Dim Mat1 As Matrix = Matrix.RotationYawPitchRoll(Rotation.X / 200.0F, -Rotation.Y / 200.0F, 0)
                Dim Mat3 As Matrix = Matrix.Translation(New Vector3(Translation.X / 50.0F, Translation.Y / 50.0F, Zoom))
                Device.Transform.World = Mat1 * Mat3

                For Phase As Integer = 0 To 1
                    For Each ModelObj As VertexList In Model_Object
                        With ModelObj
                            Dim Has_Alpha As Boolean

                            If ModelObj.Texture_ID < Model_Texture_Index.Length Then
                                Dim Texture_Name As String = Model_Texture_Index(ModelObj.Texture_ID)
                                If Model_Texture IsNot Nothing Then
                                    For Each Current_Texture As OhanaTexture In Model_Texture
                                        If Current_Texture.Name = Texture_Name And InStr(Texture_Name, "dummy") = False Then
                                            Has_Alpha = Current_Texture.Has_Alpha
                                            If Not Has_Alpha Or (Has_Alpha And Phase > 0) Then Device.SetTexture(0, Current_Texture.Texture)
                                        End If
                                    Next
                                End If
                            End If

                            If Not Has_Alpha Or (Has_Alpha And Phase > 0) Then
                                Dim Vertex_Format As VertexFormats = VertexFormats.Position Or VertexFormats.Normal Or VertexFormats.Texture1 Or VertexFormats.Diffuse
                                Dim VtxBuffer As New VertexBuffer(GetType(OhanaVertex), .Vertice.Length, Device, Usage.None, Vertex_Format, Pool.Managed)
                                VtxBuffer.SetData(.Vertice, 0, LockFlags.None)
                                Device.VertexFormat = Vertex_Format
                                Device.SetStreamSource(0, VtxBuffer, 0)
                                Dim Index_Buffer As New IndexBuffer(GetType(Integer), .Index.Length, Device, Usage.WriteOnly, Pool.Managed)
                                Index_Buffer.SetData(.Index, 0, LockFlags.None)
                                Device.Indices = Index_Buffer
                                Device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, .Vertice.Length, 0, .Index.Length / 3)
                                VtxBuffer.Dispose()
                                Index_Buffer.Dispose()
                            End If

                            Device.SetTexture(0, Nothing)
                        End With
                    Next
                Next

                Device.EndScene()
                Device.Present()
            End If

            Application.DoEvents()
        Loop
    End Sub
End Class
