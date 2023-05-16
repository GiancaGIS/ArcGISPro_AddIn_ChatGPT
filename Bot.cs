using AddInAskChatGPT.Enums;
using AddInAskChatGPT.Extensions;
using AddInAskChatGPT.Properties;
using ArcGIS.Desktop.Framework;
using OpenAI_API;
using OpenAI_API.Chat;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;


namespace AddInAskChatGPT
{
    public class Bot
    {
        private Conversation chat = null;

        public ObservableCollection<Message> Messages { get; } = new ObservableCollection<Message>();

        private static Bot instance;
        private static readonly object locker = new();
        public static Bot GetBot()
        {
            if (instance is null)
            {
                lock (locker)
                {
                    instance ??= new Bot();
                }
            }

            return instance;
        }

        protected Bot()
        {
            if (string.Equals(Settings.Default.UseAPI, UseAPI.OpenAI.GetDescription(), StringComparison.InvariantCultureIgnoreCase))
            {
                Api = new()
                {
                    Auth = new APIAuthentication(Settings.Default.OpenAI_api_key)
                };

                if (!string.IsNullOrWhiteSpace(Settings.Default.OpenAI_Organization))
                {
                    Api.Auth.OpenAIOrganization = Settings.Default.OpenAI_Organization;
                }
            }
            else if (string.Equals(Settings.Default.UseAPI, UseAPI.OpenAI_Azure.GetDescription(), StringComparison.InvariantCultureIgnoreCase))
            {
                Api = OpenAIAPI.ForAzure(Settings.Default.OpenAIAzure_ResourceName, Settings.Default.OpenAIAzure_DeploymentId, Settings.Default.OpenAIAzure_api_key);
                Api.ApiVersion = Settings.Default.OpenAIAzure_APIVersion;
            }

        }

        public OpenAIAPI Api
        {
            get; set;
        }


        public async Task SendActivityAsync(string text)
        {
            try
            {
                CreateConversationIfNotExistAsync();

                Messages.Add(new Message
                {
                    MessageFrom = MessageFrom.User,
                    Text = text,
                });

                chat.AppendUserInput(text);
                string response = await chat.GetResponseFromChatbotAsync();

                Message message = new()
                {
                    MessageFrom = MessageFrom.Bot,
                    Text = response
                };

                Messages.Add(message);

                //await foreach (var res in chat.StreamResponseEnumerableFromChatbotAsync())
                //{
                //    message.Text += res;
                //}

            }
            catch (System.Security.Authentication.AuthenticationException)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Invalid OpenAI api key set in Options -> ChatGPT!",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Messages.Add(new Message
                {
                    MessageFrom = MessageFrom.System,
                    Text = ex.Message
                });
            }
            finally
            {
                FrameworkApplication.AddNotification(new Notification()
                {
                    Title = "AddIn Ask ChatGPT",
                    Message = "Response available from ChatGPT",
                    ImageUrl = @"pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/LayerMasking32.png"
                });
            }

        }

        public void ClearChat()
        {
            Messages.Clear();
            chat = null;
        }

        private void CreateConversationIfNotExistAsync()
        {
            if (chat != null)
            {
                return;
            }

            if (this.Api is null)
                throw new System.Security.Authentication.AuthenticationException();

            chat = Api.Chat.CreateConversation();

            if (string.Equals(Settings.Default.UseAPI, UseAPI.OpenAI.GetDescription(), StringComparison.InvariantCultureIgnoreCase))
            {
                string modelName = Settings.Default.OpenAI_Model;

                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = OpenAI_API.Models.Model.DefaultModel.ModelID;
                }
                chat.Model = new OpenAI_API.Models.Model(modelName);
            }
        }

    }
}
