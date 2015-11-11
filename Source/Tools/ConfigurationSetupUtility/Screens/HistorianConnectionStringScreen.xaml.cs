﻿//******************************************************************************************************
//  HistorianConnectionStringScreen.xaml.cs - Gbtc
//
//  Copyright © 2011, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  01/11/2011 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GSF.TimeSeries.Adapters;
using GSF;
using GSF.Reflection;
using System.IO;

namespace ConfigurationSetupUtility.Screens
{
    /// <summary>
    /// Interaction logic for HistorianConnectionStringScreen.xaml
    /// </summary>
    public partial class HistorianConnectionStringScreen : UserControl, IScreen
    {
        #region [ Members ]

        // Fields
        private Dictionary<string, object> m_state;
        private Dictionary<string, PropertyInfo> m_connectionStringParameters;
        private Dictionary<string, string> m_settings;
        private bool m_suppressTextChangedEvents;
        private bool m_applyNumericValidation;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="HistorianConnectionStringScreen"/> class.
        /// </summary>
        public HistorianConnectionStringScreen()
        {
            m_connectionStringParameters = new Dictionary<string, PropertyInfo>(StringComparer.CurrentCultureIgnoreCase);
            m_settings = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
            InitializeComponent();
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the screen to be displayed when the user clicks the "Next" button.
        /// </summary>
        public IScreen NextScreen
        {
            get
            {
                return (IScreen)m_state["setupReadyScreen"];
            }
        }

        /// <summary>
        /// Gets a boolean indicating whether the user can advance to
        /// the next screen from the current screen.
        /// </summary>
        public bool CanGoForward
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a boolean indicating whether the user can return to
        /// the previous screen from the current screen.
        /// </summary>
        public bool CanGoBack
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a boolean indicating whether the user can cancel the
        /// setup process from the current screen.
        /// </summary>
        public bool CanCancel
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a boolean indicating whether the user input is valid on the current page.
        /// </summary>
        public bool UserInputIsValid
        {
            get
            {
                foreach (ListBoxItem item in GetConnectionStringParameterNamesList())
                {
                    string parameterName = item.Content.ToString();

                    if (IsRequiredParameter(parameterName) && !m_settings.ContainsKey(parameterName))
                    {
                        MessageBox.Show("Please enter a value for all required parameters (highlighted in red).");
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Collection shared among screens that represents the state of the setup.
        /// </summary>
        public Dictionary<string, object> State
        {
            get
            {
                return m_state;
            }
            set
            {
                m_state = value;
                InitializeState();
            }
        }

        /// <summary>
        /// Gets dictionary of connection string parameters.
        /// </summary>
        public Dictionary<string, PropertyInfo> ConnectionStringParameters
        {
            get
            {
                return m_connectionStringParameters;
            }
        }

        /// <summary>
        /// Allows the screen to update the navigation buttons after a change is made
        /// that would affect the user's ability to navigate to other screens.
        /// </summary>
        public Action UpdateNavigation { get; set; }

        #endregion

        #region [ Methods ]

        // Initializes the state keys to their default values.
        private void InitializeState()
        {
            string assemblyName, typeName;

            m_state["historianConnectionString"] = string.Empty;
            InitializeConnectionStringParameters();
            ParameterNameListBox.ItemsSource = GetConnectionStringParameterNamesList();

            if (ParameterNameListBox.Items.Count > 0)
                ParameterNameListBox.SelectedIndex = 0;
            
            assemblyName = m_state["historianAssemblyName"].ToNonNullString();
            typeName = m_state["historianTypeName"].ToNonNullString();

            AssemblyInfoLabel.Content = typeName + " from " + assemblyName;
        }
        
        // Initializes the collection of connection string parameters.
        private void InitializeConnectionStringParameters()
        {
            string assemblyName = m_state["historianAssemblyName"].ToString();
            string typeName = m_state["historianTypeName"].ToString();

            RefreshConnectionStringParameters(assemblyName, typeName);

            ConnectionStringTextBox.Text = string.Empty;
            m_settings.Clear();
        }

        /// <summary>
        /// Refreshes the connection string parameters.
        /// </summary>
        /// <param name="assemblyName">Assembly name to load connection string parameters from.</param>
        /// <param name="typeName">Type name to load connection string parameters from.</param>
        public void RefreshConnectionStringParameters(string assemblyName, string typeName)
        {
            m_connectionStringParameters.Clear();

            if (!string.IsNullOrWhiteSpace(assemblyName) && !string.IsNullOrWhiteSpace(typeName))
            {
                Assembly historianAssembly = Assembly.LoadFrom(assemblyName);
                Type historianType = historianAssembly.GetType(typeName);
                ConnectionStringParameterAttribute connectionStringParameterAttribute;

                foreach (PropertyInfo property in historianType.GetProperties())
                {
                    if (property.TryGetAttribute(out connectionStringParameterAttribute))
                        m_connectionStringParameters.Add(property.Name, property);
                }
            }
        }

        // Gets a list of connection string parameter names as ListBoxItems.
        private List<ListBoxItem> GetConnectionStringParameterNamesList()
        {
            return m_connectionStringParameters.Keys
                .Union(m_settings.Keys, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(name => name)
                .OrderByDescending(name => IsRequiredParameter(name))
                .Select(name => new ListBoxItem() { Content = name })
                .ToList();
        }

        // Gets the value associated with the given parameter.
        // If the parameter is in the connection string, the value from the connection string is returned.
        // If not, the default value of the given parameter is returned.
        private string GetValue(string parameterName, out Type parameterType)
        {
            DefaultValueAttribute defaultValueAttribute;
            PropertyInfo propertyInfo;
            string value;

            m_settings.TryGetValue(parameterName, out value);

            if (m_connectionStringParameters.TryGetValue(parameterName, out propertyInfo))
            {
                if (string.IsNullOrWhiteSpace(value) && propertyInfo.TryGetAttribute(out defaultValueAttribute))
                    value = defaultValueAttribute.Value.ToNonNullString();

                parameterType = propertyInfo.PropertyType;
            }
            else
                parameterType = typeof(string);

            return value;
        }

        // Determines whether the given parameter is a required parameter (it does not have a default value).
        private bool IsRequiredParameter(string parameterName)
        {
            PropertyInfo property;

            if (m_connectionStringParameters.TryGetValue(parameterName, out property))
            {
                DefaultValueAttribute defaultValueAttribute;

                if (!property.TryGetAttribute(out defaultValueAttribute))
                    return true;
            }

            return false;
        }

        // Updates all GUI elements related to the historian connection string.
        private void UpdateAll()
        {
            List<ListBoxItem> parameterNames = GetConnectionStringParameterNamesList();
            ListBoxItem selectedParameter = ParameterNameListBox.SelectedItem as ListBoxItem;
            bool suppress = m_suppressTextChangedEvents;

            // Update the list of parameter names.
            m_suppressTextChangedEvents = true;
            ParameterNameListBox.ItemsSource = parameterNames;

            // If the connection string text box is not focused, update its contents.
            if (!ConnectionStringTextBox.IsFocused)
                ConnectionStringTextBox.Text = m_settings.JoinKeyValuePairs();

            // Since the list of parameter names may have changed,
            // attempt to find the previous selection in the new list and select it.
            if (selectedParameter != null)
            {
                ListBoxItem itemToSelect = parameterNames.SingleOrDefault(parameter => parameter.Content.Equals(selectedParameter.Content));

                if (itemToSelect != null)
                    ParameterNameListBox.SelectedIndex = parameterNames.IndexOf(itemToSelect);
            }

            m_suppressTextChangedEvents = suppress;
        }

        // Changes the color of required parameters in the list box if their values are not specified.
        private void ParameterNameListBox_LayoutUpdated(object sender, EventArgs e)
        {
            foreach (ListBoxItem item in ParameterNameListBox.Items)
            {
                string name = item.Content.ToString();
                bool settingIsDefined = m_settings.ContainsKey(name);

                if (IsRequiredParameter(name) && !settingIsDefined)
                    item.Foreground = Brushes.Red;
                else
                    item.Foreground = SystemColors.ControlTextBrush;

                item.FontWeight = (settingIsDefined ? FontWeights.Bold : FontWeights.Normal);
            }
        }

        // Updates the value and description displayed on the screen when the user selects a different parameter.
        private void ParameterNameListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem selectedItem = ParameterNameListBox.SelectedItem as ListBoxItem;

            // If the parameter value text box isn't focused,
            // clear the contents the description text block.
            if (!ParameterValueTextBox.IsFocused)
                DescriptionTextBlock.Text = string.Empty;

            if (selectedItem != null)
            {
                string parameterName = selectedItem.Content.ToString();
                bool suppress = m_suppressTextChangedEvents;
                PropertyInfo property;
                DescriptionAttribute descriptionAttribute;

                // If the parameter value text box is not in focus,
                // change its contents to the value of the newly selected parameter.
                if (!ParameterValueTextBox.IsFocused)
                {
                    string value;
                    Type parameterType;

                    m_suppressTextChangedEvents = true;
                    value = GetValue(parameterName, out parameterType);

                    if (parameterType.IsEnum)
                    {
                        // Handle enumerations as a drop-down combo
                        ParameterValueTextBox.Visibility = Visibility.Collapsed;
                        ParameterValueTrueRadioButton.Visibility = Visibility.Collapsed;
                        ParameterValueFalseRadioButton.Visibility = Visibility.Collapsed;

                        ParameterValueComboBox.Items.Clear();

                        foreach (object item in Enum.GetValues(parameterType))
                        {
                            ParameterValueComboBox.Items.Add(item.ToString());
                        }
                        
                        ParameterValueComboBox.SelectedItem = value;
                        ParameterValueComboBox.Visibility = Visibility.Visible;
                    }
                    else if (parameterType == typeof(bool))
                    {
                        // Handle boolean values with a radio button group
                        ParameterValueTextBox.Visibility = Visibility.Collapsed;
                        ParameterValueComboBox.Visibility = Visibility.Collapsed;

                        if (string.IsNullOrWhiteSpace(value))
                        {
                            ParameterValueTrueRadioButton.IsChecked = false;
                            ParameterValueFalseRadioButton.IsChecked = false;
                        }
                        else
                        {
                            if (value.ParseBoolean())
                                ParameterValueTrueRadioButton.IsChecked = true;
                            else
                                ParameterValueFalseRadioButton.IsChecked = true;
                        }

                        ParameterValueTrueRadioButton.Visibility = Visibility.Visible;
                        ParameterValueFalseRadioButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Handle string values with a text box
                        ParameterValueComboBox.Visibility = Visibility.Collapsed;
                        ParameterValueTrueRadioButton.Visibility = Visibility.Collapsed;
                        ParameterValueFalseRadioButton.Visibility = Visibility.Collapsed;
                        ParameterValueTextBox.Text = value;
                        m_applyNumericValidation = parameterType.IsNumeric();
                        ParameterValueTextBox.Visibility = Visibility.Visible;
                    }

                    m_suppressTextChangedEvents = suppress;
                }

                DescriptionTextBlock.Text = string.Empty;

                // Update the description text block to the description of the newly seleted parameter.
                if (m_connectionStringParameters.TryGetValue(parameterName, out property))
                {
                    if (property.TryGetAttribute(out descriptionAttribute))
                        DescriptionTextBlock.Text = descriptionAttribute.Description;
                }
            }
        }

        // Updates the connection string when the value of a parameter is changed by the user.
        private void ParameterValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ListBoxItem selectedItem = ParameterNameListBox.SelectedItem as ListBoxItem;

            if (!m_suppressTextChangedEvents && selectedItem != null)
            {
                string parameterName = selectedItem.Content.ToString();
                string value = ParameterValueTextBox.Text;

                if (string.IsNullOrWhiteSpace(value))
                {
                    m_settings.Remove(parameterName);
                }
                else
                {
                    if (m_applyNumericValidation && !Common.IsNumeric(value))
                        ParameterValueTextBox.Text = value.RemoveCharacters(chr => !(Char.IsDigit(chr) || chr == '.' || chr == '+' || chr == '-' || Char.ToLower(chr) == 'e'));
                    else
                        m_settings[parameterName] = value;
                }

                UpdateAll();
            }
        }

        // Updates the connection string when the value of a parameter is changed by the user.
        private void ParameterValueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem selectedItem = ParameterNameListBox.SelectedItem as ListBoxItem;

            if (!m_suppressTextChangedEvents && selectedItem != null)
            {
                string parameterName = selectedItem.Content.ToString();
                string value = ParameterValueComboBox.SelectedItem.ToNonNullString();

                if (string.IsNullOrWhiteSpace(value))
                    m_settings.Remove(parameterName);
                else
                    m_settings[parameterName] = value;

                UpdateAll();
            }
        }

        // Updates the connection string when the value of a parameter is changed by the user.
        private void ParameterValueTrueRadioButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            BooleanParameterValueChecked(true);
        }

        private void ParameterValueFalseRadioButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            BooleanParameterValueChecked(false);
        }

        private void BooleanParameterValueChecked(bool value)
        {
            ListBoxItem selectedItem = ParameterNameListBox.SelectedItem as ListBoxItem;

            if (!m_suppressTextChangedEvents && selectedItem != null)
            {
                string parameterName = selectedItem.Content.ToString();

                m_settings[parameterName] = value.ToString();

                UpdateAll();
            }
        }

        // Removes a value from the connection string when the user chooses to use the default value.
        private void DefaultButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ListBoxItem selectedItem = ParameterNameListBox.SelectedItem as ListBoxItem;

            if (selectedItem != null)
            {
                m_settings.Remove(selectedItem.Content.ToString());
                UpdateAll();
            }
        }

        // Updates the connection string when the user chooses to modify the connection string directly.
        private void ConnectionStringTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string text = ConnectionStringTextBox.Text;

                // Update settings to reflect the connection string.
                m_settings = text.ParseKeyValuePairs();
                m_state["historianConnectionString"] = text;

                // Update everything.
                if (!m_suppressTextChangedEvents)
                    UpdateAll();

                // Change the foreground to black if there were no errors in parsing.
                ConnectionStringTextBox.Foreground = SystemColors.ControlTextBrush;
            }
            catch
            {
                // Don't fail if parsing fails, but make the text red to notify the user.
                ConnectionStringTextBox.Foreground = Brushes.Red;
            }
        }

        #endregion
    }
}
