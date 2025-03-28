﻿// See https://aka.ms/new-console-template for more information
// lembrar de dar build para passar os args direito
using System; //tem as libs gerais
using System.Globalization; //para usar . no lugar da ,
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Desafio_INOA.Services;

namespace Desafio_INOA
{
    public class Program
    {
        public static async Task Main(string[] args) //já inicia com uma string de argumentos (3 argumentos devem ser passados)
        {
            Console.WriteLine("Bem-vindos ao Monitoramento de Cotação de Ativo da B3\n");
            Console.WriteLine("--------------------------------------------------\n");

            if (args.Length != 3) //caso a passem os parametro errados no console
            {
                Console.WriteLine("Uso: MonitorB3 <ativo> <preco_venda> <preco_compra>");
                Console.WriteLine("Exemplo: MonitorB3 PETR4 22.67 22.59\n"); //exemplo passado pelo Desafio
                return;
            }

            string ativo = args[0]; //pega o primeiro argumento
            if ( //funçao de conversão do string para decimal
                !decimal.TryParse(
                    args[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal precoVenda
                )
            )
            {
                Console.WriteLine(
                    "Erro: O preço de venda deve ser um número decimal válido (use ponto como separador)."
                );
                return;
            }
            if (
                !decimal.TryParse(
                    args[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal precoCompra
                )
            )
            {
                Console.WriteLine(
                    "Erro: O preço de compra deve ser um número decimal válido (use ponto como separador)."
                );
                return;
            } //fim funçao de conversão do string para decimal

            Console.WriteLine($"Monitorando o ativo: {ativo}");
            Console.WriteLine($"Preço de venda de referência: {precoVenda:N2}");
            Console.WriteLine($"Preço de compra de referência: {precoCompra:N2}\n");

            //configuracoes do serviço de email
            Configuracao? config = LerConfiguracao("../../../config.json");
            //é necessário voltar 3 pastas pra ficar onde o json tá agora. escolhi assim por motivos de github
            if (config == null)
            {
                Console.WriteLine("Erro ao carregar o arquivo de configuracao.");
                return;
            }

            //instanciando emailservice
            EmailService emailService = new EmailService();

            Console.WriteLine("Enviando o e-mail...\n"); //checagem de seguranca se chegou ate aqui

            // carregar configurações do email do json
            await emailService.SendEmail(
                config.SmtpServidor,
                config.SmtpPorta,
                config.SmtpSSL,
                config.EmailRemetente,
                config.SenhaRemetente,
                config.EmailDestino,
                "Monitor B3 - Servidor em execução",
                $"O programa de monitoramento está ativo e recebeu 3 parametros: Ativo={ativo}, Venda={precoVenda}, Compra={precoCompra}.\n\nVocê receberá novos e-mails assim que o programa atingir os parametro estabelecidos."
            );

            Console.WriteLine(
                "\nIniciando o monitoramento contínuo (pressione Ctrl+C para sair)...\n"
            );

            // PARTE DO MONITORAMENTO E INTEGRAÇÃO COM A API
            AlphaVantageService alphaVantageService = new AlphaVantageService();

            while (true)
            {
                int tempo = 3; // tempo de intervalo entre as verificações
                decimal? cotacaoAtual = await alphaVantageService.ObterCotacaoAsync($"{ativo}.SA"); //adicionamos .SA para as ações da B3

                if (cotacaoAtual.HasValue) //verifica se deu certo a obtenção da cotação
                {
                    Console.WriteLine(
                        $"[{DateTime.Now}] Cotação atual de {ativo}: {cotacaoAtual.Value:N2}."
                    ); //data e hora da cotação atual
                    Console.WriteLine($"Próxima verificação em {tempo} minuto(s).\n");

                    //lógica de comparação e envio de e-mail
                    if (cotacaoAtual > precoVenda)
                    {
                        string assunto = $"\nALERTA DE VENDA: {ativo} atingiu {cotacaoAtual:N2}\n";
                        string corpo =
                            $"A cotação de {ativo} subiu para {cotacaoAtual:N2}, acima do preço de venda de referência ({precoVenda:N2}).\nConsidere vender.";
                        await emailService.SendEmail(
                            config.SmtpServidor,
                            config.SmtpPorta,
                            config.SmtpSSL,
                            config.EmailRemetente,
                            config.SenhaRemetente,
                            config.EmailDestino,
                            assunto,
                            corpo
                        );
                        Console.WriteLine(
                            $"[{DateTime.Now}] E-mail de alerta de VENDA enviado para {config.EmailDestino}"
                        );
                    }
                    else if (cotacaoAtual < precoCompra)
                    {
                        string assunto = $"\nALERTA DE COMPRA: {ativo} atingiu {cotacaoAtual:N2}\n";
                        string corpo =
                            $"A cotação de {ativo} caiu para {cotacaoAtual:N2}, abaixo do preço de compra de referência ({precoCompra:N2}).\nConsidere comprar.";
                        await emailService.SendEmail(
                            config.SmtpServidor,
                            config.SmtpPorta,
                            config.SmtpSSL,
                            config.EmailRemetente,
                            config.SenhaRemetente,
                            config.EmailDestino,
                            assunto,
                            corpo
                        );
                        Console.WriteLine(
                            $"\n[{DateTime.Now}] E-mail de alerta de COMPRA enviado para {config.EmailDestino}\n"
                        );
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now}] Falha ao obter a cotação de {ativo}.");
                }

                await Task.Delay(TimeSpan.FromMinutes(tempo)); // se quiser altera pra FromHours ou Seconds. mas altera o texto também que avisa o tempo
            }
        }

        static Configuracao? LerConfiguracao(string caminhoArquivo)
        {
            try
            {
                string jsonString = File.ReadAllText(caminhoArquivo);
                return JsonSerializer.Deserialize<Configuracao>(jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ler o arquivo de configuração: {ex.Message}");
                return null;
            }
        }
    }

    public class Configuracao //caso o json esteja vazio, sem argmentos, ele pode gerar um erro e complicar o tempo de execucao por nao ter sido inicializado. Required faz dar erro logo se vier nulo
    {
        public required string EmailDestino { get; set; }
        public required string SmtpServidor { get; set; }
        public int SmtpPorta { get; set; }
        public bool SmtpSSL { get; set; }
        public required string EmailRemetente { get; set; }
        public required string SenhaRemetente { get; set; }
    }
}
