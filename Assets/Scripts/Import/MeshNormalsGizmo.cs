using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;

public class MeshNormalsGizmo : MonoBehaviour
{
    private Mesh _mesh = null;

    void Start()
    {
        MeshFilter filter = GetComponent<MeshFilter>();

        if (filter != null)
        {
            _mesh = filter.sharedMesh;
        }
    }

    private void OnDrawGizmos()
    {
        if (_mesh == null)
        {
            return;
        }

        for (int count = 0; count < _mesh.vertexCount; count++)
        {
            var vert = transform.TransformPoint(_mesh.vertices[count]);
            var normal = transform.TransformDirection(_mesh.normals[count]);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(vert, 0.25f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(vert, vert + (normal * 10f));
        }
    }
}
