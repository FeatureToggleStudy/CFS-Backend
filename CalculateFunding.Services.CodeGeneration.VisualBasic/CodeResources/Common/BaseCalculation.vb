﻿Public Class BaseCalculation

    Public Property Provider() as Provider

#Region "Legacy Store Support"

    public Property rid as String
    Public Property currentscenario As Scenario

    Public Sub Print(Of T) (value As T, name As String, rid As String)
        
    End Sub

    public Function LAToProv(Of T)(value as T) As T
        Return value
    End Function

    public Function IIf(Of T)(value as T, one As Boolean, two as Boolean) As Boolean
        Return value
    End Function

    Public Sub Exclude (rid As String)
        
    End Sub

#End Region

End Class