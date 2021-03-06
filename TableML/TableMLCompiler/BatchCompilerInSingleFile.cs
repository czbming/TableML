﻿#region Copyright (c) 2015 KEngine / Kelly <http://github.com/mr-kelly>, All rights reserved.

// KEngine - Toolset and framework for Unity3D
// ===================================
// 
// Filename: SettingModuleEditor.cs
// Date:     2015/12/03
// Author:  Kelly
// Email: 23110388@qq.com
// Github: https://github.com/mr-kelly/KEngine
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DotLiquid;
using NPOI.Util;

namespace TableML.Compiler
{
    /// <summary>
    /// BatchCompiler , compile
    /// 扩展BatchCopiler：每张表对应一个代码文件
    /// </summary>
    public partial class BatchCompiler
    {
        /// <summary>
        /// 缺省时，默认生成代码存放的路径
        /// </summary>
        public const string DefaultGenCodeDir = "GenCode\\";
        /// <summary>
        /// tab表所有单例类的Manger Class
        /// </summary>
        public const string ManagerClassName = "SettingsManager.cs";
        /// <summary>
        /// 生成代码的文件名=表名+后缀+.cs，建议和模版中的一致
        /// </summary>
        public const string FileNameSuffix = "Setting";

        /// <summary>
        /// 处理文件名，符合微软的C#命名风格
        /// copy from TableTemplateVars.DefaultClassNameParse
        /// </summary>
        /// <param name="tabFilePath"></param>
        /// <returns></returns>
        public string DefaultClassNameParse(string tabFilePath)
        {
            // 未处理路径的类名, 去掉后缀扩展名
            var classNameOrigin = Path.ChangeExtension(tabFilePath, null);

            // 子目录合并，首字母大写, 组成class name
            var className = classNameOrigin.Replace("/", "_").Replace("\\", "_");
            className = className.Replace(" ", "");
            className = string.Join("", (from name
                in className.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                                         select (name[0].ToString().ToUpper() + name.Substring(1, name.Length - 1)))
                .ToArray());

            // 去掉+或#号后面的字符
            var plusSignIndex = className.IndexOf("+");
            className = className.Substring(0, plusSignIndex == -1 ? className.Length : plusSignIndex);
            plusSignIndex = className.IndexOf("#");
            className = className.Substring(0, plusSignIndex == -1 ? className.Length : plusSignIndex);

            return className;

        }

        /// <summary>
        /// 生成代码文件
        /// </summary>
        /// <param name="results"></param>
        /// <param name="settingCodeIgnorePattern"></param>
        /// <param name="forceAll"></param>
        /// <param name="genManagerClass"></param>
        /// <param name="templateVars">如果是生成Manager Class 一定要在外部初始化此字段</param>
        public void GenCodeFile(TableCompileResult compileResult, string genCodeTemplateString, string genCodeFilePath,
            string nameSpace = "AppSettings", string changeExtension = ".tml", string settingCodeIgnorePattern = null, bool forceAll = false, bool genManagerClass = false, Dictionary<string, TableTemplateVars> templateVars = null)
        {
            // 根据编译结果，构建vars，同class名字的，进行合并
            if (!genManagerClass)
            {
                templateVars = new Dictionary<string, TableTemplateVars>();
            }
            if (!string.IsNullOrEmpty(settingCodeIgnorePattern))
            {
                var ignoreRegex = new Regex(settingCodeIgnorePattern);
                if (ignoreRegex.IsMatch(compileResult.TabFileRelativePath))
                    return; // ignore this 
            }

            var customExtraStr = CustomExtraString != null ? CustomExtraString(compileResult) : null;

            var templateVar = new TableTemplateVars(compileResult, customExtraStr);

            // 尝试类过滤
            var ignoreThisClassName = false;
            if (GenerateCodeFilesFilter != null)
            {
                for (var i = 0; i < GenerateCodeFilesFilter.Length; i++)
                {
                    var filterClass = GenerateCodeFilesFilter[i];
                    if (templateVar.ClassName.Contains(filterClass))
                    {
                        ignoreThisClassName = true;
                        break;
                    }

                }
            }
            if (!ignoreThisClassName)
            {
                if (!templateVars.ContainsKey(templateVar.ClassName))
                {
                    templateVars.Add(templateVar.ClassName, templateVar);
                }
                else
                {
                    templateVars[templateVar.ClassName].RelativePaths.Add(compileResult.TabFileRelativePath);
                }
            }

            if (!genManagerClass)
            {
                //首字母大写，符合微软命名规范
                var newFileName = string.Concat(DefaultClassNameParse(compileResult.TabFileRelativePath), FileNameSuffix, ".cs");
                if (string.IsNullOrEmpty(genCodeFilePath))
                {
                    genCodeFilePath += string.Concat(DefaultGenCodeDir, newFileName);
                }
                else
                {
                    genCodeFilePath += newFileName;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(genCodeFilePath))
                {
                    genCodeFilePath += string.Concat(DefaultGenCodeDir, ManagerClassName);
                }
                else
                {
                    genCodeFilePath += ManagerClassName;
                }
            }


            // 整合成字符串模版使用的List
            var templateHashes = new List<Hash>();
            foreach (var kv in templateVars)
            {
                //NOTE render 加多一项TabFilName
                var templateVar2 = kv.Value;
                var renderTemplateHash = Hash.FromAnonymousObject(templateVar2);
                templateHashes.Add(renderTemplateHash);
            }

            if (forceAll)
            {
                // force 才进行代码编译
                GenerateCode(genCodeTemplateString, genCodeFilePath, nameSpace, templateHashes);
            }
        }


        void GenManagerClass(List<TableCompileResult> results, string genCodeTemplateString, string genCodeFilePath,
            string nameSpace = "AppSettings", string changeExtension = ".tml", string settingCodeIgnorePattern = null, bool forceAll = false)
        {
            //保存所有的编译结果，用来生成ManagerClass
            var templateVars = new Dictionary<string, TableTemplateVars>();
            foreach (var compileResult in results)
            {
                GenCodeFile(compileResult, genCodeTemplateString, genCodeFilePath, nameSpace, changeExtension, settingCodeIgnorePattern, forceAll, true, templateVars);
            }
        }


        /// <summary>
        /// 编译所有的文件，并且每个文件生成一个代码文件
        /// Compile one directory 's all settings, and return behaivour results
        /// </summary>
        /// <param name="sourcePath">需要的编译的Excel路径</param>
        /// <param name="compilePath">编译后的tml存放路径</param>
        /// <param name="genCodeFilePath">编译后的代码存放路径</param>
        /// <param name="changeExtension">编译后的tml文件格式</param>
        /// <param name="forceAll">no diff! only force compile will generate code</param>
        /// <param name="genCSCode">是否生成CSharp代码</param>
        /// <returns></returns>
        public List<TableCompileResult> CompileAll(string sourcePath, string compilePath,
            string genCodeFilePath, string genCodeTemplateString = null, string nameSpace = "AppSettings",
            string changeExtension = ".tml", string settingCodeIgnorePattern = null, bool forceAll = false, bool genCSCode = true)
        {
            var results = new List<TableCompileResult>();
            var compileBaseDir = compilePath;
            // excel compiler
            var compiler = new Compiler(new CompilerConfig() { ConditionVars = CompileSettingConditionVars });

            var excelExt = new HashSet<string>() { ".xls", ".xlsx", ".tsv" };
            var copyExt = new HashSet<string>() { ".txt" };
            if (Directory.Exists(sourcePath) == false)
            {
                Console.WriteLine("Error! {0} 路径不存在！", sourcePath);
                return results;
            }
            var findDir = sourcePath;
            try
            {
                Dictionary<string, string> dst2src = new Dictionary<string, string>();
                var allFiles = Directory.GetFiles(findDir, "*.*", SearchOption.AllDirectories);
                var nowFileIndex = -1; // 开头+1， 起始为0
                foreach (var excelPath in allFiles)
                {
                    //TODO 如果要读取每一个sheet
                    nowFileIndex++;
                    var ext = Path.GetExtension(excelPath);
                    var fileName = Path.GetFileNameWithoutExtension(excelPath);

                    var relativePath = excelPath.Replace(findDir, "").Replace("\\", "/");
                    if (relativePath.StartsWith("/"))
                        relativePath = relativePath.Substring(1);
                    if (excelExt.Contains(ext) && !fileName.StartsWith("~")) // ~开头为excel临时文件，不要读
                    {
                        // it's an excel file
                        /*
                         * NOTE 开始编译Excel 成 tml文件
                         * 每编译一个Excel就生成一个代码文件
                        */
                        //NOTE 设置编译后文件的文件名(tml文件名)
                        if (Path.GetExtension(excelPath) == ".tsv")
                        {
                            relativePath = Path.GetFileName(excelPath);
                        }
                        else
                        {
                            relativePath = SimpleExcelFile.GetOutFileName(excelPath);
                        }
                        if (string.IsNullOrEmpty(relativePath))
                        {
                            ConsoleHelper.Error("{0} 输出文件名为空，跳过", fileName);
                            continue;
                        }
                        var compileToPath = string.Format("{0}/{1}", compileBaseDir,
                            Path.ChangeExtension(relativePath, changeExtension));
                        var srcFileInfo = new FileInfo(excelPath);
                        var dstFileName = Path.GetFileNameWithoutExtension(compileToPath);
                        if (dst2src.ContainsKey(dstFileName) == false)
                        {
                            dst2src.Add(dstFileName, Path.GetFileName(excelPath));
                        }
                        Console.WriteLine("Compiling Excel to Tab..." +
                                          string.Format("{0} -> {1}", excelPath, compileToPath));

                        // 如果已经存在，判断修改时间是否一致，用此来判断是否无需compile，节省时间
                        bool doCompile = true;
                        if (File.Exists(compileToPath))
                        {
                            var toFileInfo = new FileInfo(compileToPath);

                            if (!forceAll && srcFileInfo.LastWriteTime == toFileInfo.LastWriteTime)
                            {
                                //Log.DoLog("Pass!SameTime! From {0} to {1}", excelPath, compileToPath);
                                doCompile = false;
                            }
                        }
                        if (doCompile)
                        {
                            Console.WriteLine("[SettingModule]Compile from {0} to {1}", excelPath, compileToPath);
                            Console.WriteLine(); //美观一下 打印空白行
                            var compileResult = compiler.Compile(excelPath, compileToPath, 0, compileBaseDir, doCompile);
                            if (genCSCode)
                            {
                                // 添加模板值
                                results.Add(compileResult);

                                var compiledFileInfo = new FileInfo(compileToPath);
                                compiledFileInfo.LastWriteTime = srcFileInfo.LastWriteTime;
                                //仅仅是生成单个Class，只需要当前的CompileResult
                                GenCodeFile(compileResult, genCodeTemplateString, genCodeFilePath, nameSpace,
                                    changeExtension, settingCodeIgnorePattern, forceAll);
                            }

                        }
                    }
                    else if (copyExt.Contains(ext)) // .txt file, just copy
                    {
                        // just copy the files with these ext
                        var compileToPath = string.Format("{0}/{1}", compileBaseDir,
                            relativePath);
                        var compileToDir = Path.GetDirectoryName(compileToPath);
                        if (!Directory.Exists(compileToDir))
                            Directory.CreateDirectory(compileToDir);
                        File.Copy(excelPath, compileToPath, true);

                        Console.WriteLine("Copy File ..." + string.Format("{0} -> {1}", excelPath, compileToPath));
                    }
                }
                if (genCSCode)
                {
                    //生成Manager class
                    GenManagerClass(results, DefaultTemplate.GenManagerCodeTemplate, genCodeFilePath, nameSpace,
                        changeExtension, settingCodeIgnorePattern, forceAll);
                }
                SaveCompileResult(dst2src);
            }
            finally
            {
                //EditorUtility.ClearProgressBar();
            }
            return results;
        }

        /// <summary>
        /// NOTE 目前我们的源始excel文件后和编译后的不一样，把结果输出到文件作个记录
        /// </summary>
        /// <param name="dst2Src"></param>
        public static void SaveCompileResult(Dictionary<string, string> dst2Src)
        {
            if (dst2Src == null)
            {
                return;
            }
            var startPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            //下面获取到的路径都是启动的路径，而不是exe所在的路径
//            var startPath = AppDomain.CurrentDomain.BaseDirectory;
//            var startPath = System.Environment.CurrentDirectory;
            var savePath = startPath + "/" + "compile_result.csv";
            if (File.Exists(savePath) )
            {
                File.Delete(savePath);
            }
 
            using (var sw = File.CreateText(savePath))
            {
                sw.WriteLine("[dst]目标表名,[src]源始Excel文件名");
                foreach (KeyValuePair<string, string> kv in dst2Src)
                {
                    sw.WriteLine("{0},{1}", kv.Key, kv.Value);
                }
            }
            ConsoleHelper.ConfirmationWithBlankLine("共编译{0}表，编译结果保存在：{1}", dst2Src.Count, savePath);
        }


    }
}