using System;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
using Sandplay.Data;
using UnityEngine;
namespace Sandplay.Tests
{
    public class CloudBackupTests
    {
        [Test] public void DownloadChecksumMustMatchBeforeRestore()
        {
            var method=typeof(BackendClient).GetMethod("DecodeCloudTable",BindingFlags.NonPublic|BindingFlags.Static);
            var version=new CloudTableVersion{schema_version=1,payload_json="{\"SessionName\":\"Backup\"}",checksum="wrong"};
            Assert.Throws<TargetInvocationException>(()=>method.Invoke(null,new object[]{version}));
            using var hash=System.Security.Cryptography.SHA256.Create();
            version.checksum=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(version.payload_json))).Replace("-","").ToLowerInvariant();
            Assert.AreEqual("Backup",((SessionData)method.Invoke(null,new object[]{version})).SessionName);
            version.schema_version=2;
            Assert.Throws<TargetInvocationException>(()=>method.Invoke(null,new object[]{version}));
        }
    }
}
