using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Policy;
using System.Security.Permissions;

namespace InContex.Security
{
    public class AssemblyLoader
    {
        [DllImport("mscoree.dll", CharSet = CharSet.Unicode)]
        private static extern bool StrongNameSignatureVerificationEx(string wszFilePath, bool fForceVerification, ref bool pfWasVerified);

        /// <summary>
        /// Verify if an assemblies public key is valid InfoHub key without loading the assembly.
        /// </summary>
        /// <param name="assemblyFileName">Full path and filename of the assembly to check.</param>
        /// <param name="expectedToken">The expected public key to compare against.</param>
        /// <returns></returns>
        private static bool VerifyPublicKeyToken(string assemblyFileName, byte[] expectedToken)
        {

            if(string.IsNullOrWhiteSpace(assemblyFileName))
            {
                throw new ArgumentException("Null or empty string is not a valid value for argument ‘assembly’");
            }

            byte[] assemblyToken = null;

            try
            {
                assemblyToken = AssemblyName.GetAssemblyName(assemblyFileName).GetPublicKeyToken();
            }
            catch (FileNotFoundException fileException)
            {
                string message = string.Format("Failed to verify assembly ‘{0}’. The provided file name could not be found.", assemblyFileName);
                throw new ApplicationException(message, fileException);
            }
            catch (BadImageFormatException badImageException)
            {
                string message = string.Format("The given file ‘{0}’ is not a valid assembly file.", assemblyFileName);
                throw new ApplicationException(message, badImageException);
            }

            // Compare it to the given token
            if (assemblyToken.Length != expectedToken.Length)
                return false;

            for (int i = 0; i < assemblyToken.Length; i++)
                if (assemblyToken[i] != expectedToken[i])
                    return false;

            return true;

        }

        /// <summary>
        /// Verify the assembly public key token before loading assembly.
        /// </summary>
        /// <param name="assemblyFileName">The file name of the assembly to check.</param>
        /// <returns></returns>
        private static bool VerifyAssemblyToken(string assemblyFileName)
        {
            bool notForced = false;

            // First verify that target assembly has a valid strong name signature before even trying to load it.
            bool verifiedValidStrongNameExists = StrongNameSignatureVerificationEx(assemblyFileName, false, ref notForced);

            if (verifiedValidStrongNameExists)
            {

                // Next compare public key tokens and ensure it matches
                Assembly currentAssembly = typeof(AssemblyLoader).Assembly;
                byte[] expectedToken = currentAssembly.GetName().GetPublicKeyToken();

                bool verifiedPublicKeyToken = VerifyPublicKeyToken(assemblyFileName, expectedToken);

                return verifiedPublicKeyToken;
            }
            else
            {
                return false;
            }

        }

        /// <summary>
        /// Method retrieves the strong name of the target assembly. 
        /// </summary>
        /// <param name="assembly">The name of the assembly for which to retrieve the strong name.</param>
        /// <returns></returns>
        private static StrongName GetAssemblyStrongName(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }

            AssemblyName assemblyName = assembly.GetName();

            // get the public key blob
            byte[] publicKey = assemblyName.GetPublicKey();

            if (publicKey == null || publicKey.Length == 0)
            {
                throw new InvalidOperationException(String.Format("{0} is not strongly named", assembly));
            }

            StrongNamePublicKeyBlob keyBlob = new StrongNamePublicKeyBlob(publicKey);

            return new StrongName(keyBlob, assemblyName.Name, assemblyName.Version);
        }

        /// <summary>
        /// Load specified assembly but not before performing some basic validation checks.
        /// </summary>
        /// <param name="assemblyFileName">The filename of the assembly to load.</param>
        /// <returns></returns>
        public static Assembly Load(string assemblyFileName)
        {
            bool verifiedToken = VerifyAssemblyToken(assemblyFileName);

            if(!verifiedToken)
            {
                string message = "The specified assembly ‘{0}’ does not meet application trust requirements.";
                throw new ApplicationException(message);
            }

            /*
                The first initial check that we can perform without loading the assemble has been executed. We now know that 
                the assembly has a valid strong name signature and that the public key token is valid. 
                
                Next, we load the assembly and verify if the strong name matches main applications strong name.
            */

            AssemblyName an = AssemblyName.GetAssemblyName(assemblyFileName);
            Assembly assembly = Assembly.Load(an);
            Assembly currentAssembly = typeof(AssemblyLoader).Assembly;

            StrongName targetAssemblyNameStrongName = GetAssemblyStrongName(assembly);
            StrongName expectedAssemblyStrongName = GetAssemblyStrongName(currentAssembly);

            if(targetAssemblyNameStrongName.PublicKey != expectedAssemblyStrongName.PublicKey)
            {
                string message = "Assembly does not meet security requirements. Either an invalid assembly was provided or assembly has been compromised.";
                throw new ApplicationException(message);
            }

            return assembly;
        }

    }
}
