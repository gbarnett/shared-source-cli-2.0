//------------------------------------------------------------------------------
// <copyright file="AttributeProviderAttribute.cs" company="Microsoft">
//     
//      Copyright (c) 2006 Microsoft Corporation.  All rights reserved.
//     
//      The use and distribution terms for this software are contained in the file
//      named license.txt, which can be found in the root of this distribution.
//      By using this software in any fashion, you are agreeing to be bound by the
//      terms of this license.
//     
//      You must not remove this notice, or any other, from this software.
//     
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace System.ComponentModel 
{

    using System;
    using System.Security.Permissions;

    /// <include file='doc\AttributeProviderAttribute.uex' path='docs/doc[@for="AttributeProviderAttribute"]/*' />
    /// <devdoc>
    /// </devdoc>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1813:AvoidUnsealedAttributes")]
    [AttributeUsage(AttributeTargets.Property)]
    public class AttributeProviderAttribute : Attribute 
    {
        private string _typeName;
        private string _propertyName;

        /// <include file='doc\AttributeProviderAttribute.uex' path='docs/doc[@for="AttributeProviderAttribute.AttributeProviderAttribute"]/*' />
        /// <devdoc>
        ///     Creates a new AttributeProviderAttribute object.
        /// </devdoc>
        public AttributeProviderAttribute(string typeName)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException("typeName");
            }

            _typeName = typeName;
        }

        /// <include file='doc\AttributeProviderAttribute.uex' path='docs/doc[@for="AttributeProviderAttribute.AttributeProviderAttribute"]/*' />
        /// <devdoc>
        ///     Creates a new AttributeProviderAttribute object.
        /// </devdoc>
        public AttributeProviderAttribute(string typeName, string propertyName) {
            if (typeName == null) {
                throw new ArgumentNullException("typeName");
            }
            if (propertyName == null) {
                throw new ArgumentNullException("propertyName");
            }

            _typeName = typeName;
			_propertyName = propertyName;
        }

        /// <include file='doc\AttributeProviderAttribute.uex' path='docs/doc[@for="AttributeProviderAttribute.AttributeProviderAttribute1"]/*' />
        /// <devdoc>
        ///     Creates a new AttributeProviderAttribute object.
        /// </devdoc>
        public AttributeProviderAttribute(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            _typeName = type.AssemblyQualifiedName;
        }

        /// <include file='doc\AttributeProviderAttribute.uex' path='docs/doc[@for="AttributeProviderAttribute.TypeName"]/*' />
        /// <devdoc>
        ///     The TypeName property returns the assembly qualified type name 
        ///     passed into the constructor.
        /// </devdoc>
        public string TypeName
        {
            get
            {
                return _typeName;
            }
        }

        /// <include file='doc\AttributeProviderAttribute.uex' path='docs/doc[@for="AttributeProviderAttribute.TypeName"]/*' />
        /// <devdoc>
        ///     The TypeName property returns the property name that will be used to query attributes from.
        /// </devdoc>
        public string PropertyName {
            get {
                return _propertyName;
            }
        }
    }
}

