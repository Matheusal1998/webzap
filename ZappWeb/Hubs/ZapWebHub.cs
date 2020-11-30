using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZapWeb.Database;
using ZapWeb.Models;

namespace ZapWeb.Hubs
{
    public class ZapWebHub : Hub
    {
        private BancoContext _banco;
        public ZapWebHub(BancoContext bancoContext)
        {
            _banco = bancoContext;
        }
        public async Task Cadastrar(Usuario usuario)
        {
            bool IsExiste = _banco.Usuarios.Where(x => x.Email == usuario.Email).Count() > 0;

            if (IsExiste == true) {
                await Clients.Caller.SendAsync("ReceberCadastro", false, null, "Email já cadastrado");
            }
            else
            {
                _banco.Usuarios.Add(usuario);
                _banco.SaveChanges();
                await Clients.Caller.SendAsync("ReceberCadastro", true, usuario, "Usuario cadastrado com sucesso");

            }
        }

        public async Task Logout(Usuario usuario)
        {
            var usuarioDB = _banco.Usuarios.Find(usuario.Id);
            usuarioDB.IsOnline = false;
            _banco.Usuarios.Update(usuarioDB);
            _banco.SaveChanges();

            await DeleteConnectionIdDoUsuario(usuarioDB);
            await Clients.All.SendAsync("ReceberListaUsuarios", _banco.Usuarios.ToList());
        }

        public async Task Login(Usuario usuario)
        {
            var usuarioDb = _banco.Usuarios.FirstOrDefault(x => x.Email == usuario.Email && x.Senha == usuario.Senha);

            if (usuarioDb == null)
            {
                await Clients.Caller.SendAsync("ReceberLogin", false, null, "Email ou Senha não correspondem");

            }
            else
            {
                await Clients.Caller.SendAsync("ReceberLogin", true, usuarioDb, null);

                usuarioDb.IsOnline = true;
                _banco.Usuarios.Update(usuarioDb);
                _banco.SaveChanges();

                await Clients.All.SendAsync("ReceberListaUsuarios", _banco.Usuarios.ToList());

            }

        }

        public async Task AddConnectionIdDoUsuario(Usuario usuario)
        {
            var ConnectionIdCurrent = Context.ConnectionId;
            List<string> connectionsId = null;

            Usuario usuarioDb = new Usuario();
            usuarioDb = _banco.Usuarios.Where(a => a.Id == usuario.Id).FirstOrDefault();

            if (usuarioDb.ConnectionId == null || usuarioDb.ConnectionId == "[]")
            {
                connectionsId = new List<string>();
                connectionsId.Add(ConnectionIdCurrent);


            }
            else
            {

                connectionsId = JsonConvert.DeserializeObject<List<string>>(usuarioDb.ConnectionId);
                if (!connectionsId.Contains(ConnectionIdCurrent))
                {
                    connectionsId.Add(ConnectionIdCurrent);
                }


            }


            usuarioDb.ConnectionId = JsonConvert.SerializeObject(connectionsId);
            _banco.Usuarios.Update(usuarioDb);
            _banco.SaveChanges();

            // adicionar connectionId aos grupos do signalr

            var grupos = _banco.Grupos.Where(a => a.Usuarios.Contains(usuarioDb.Email));
            foreach (var connectionId in connectionsId)
            {
                foreach (var grupo in grupos)
                {
                    await Groups.AddToGroupAsync(connectionId, grupo.Nome);
                }

            }


        }
        public async Task DeleteConnectionIdDoUsuario(Usuario usuario)
        {
            Usuario usuarioDb = _banco.Usuarios.Find(usuario.Id);
            List<string> connectionsId = null;
            if (usuarioDb.ConnectionId.Length > 0)
            {
                var ConnectionIdCurrent = Context.ConnectionId;
                connectionsId = JsonConvert.DeserializeObject<List<String>>(usuarioDb.ConnectionId);

                if (connectionsId.Contains(ConnectionIdCurrent))
                {
                    connectionsId.Remove(ConnectionIdCurrent);
                }

                if (connectionsId.Count() <= 0)
                {
                    usuarioDb.IsOnline = false;
                    await ObterListaUsuarios();
                }

                usuarioDb.ConnectionId = JsonConvert.SerializeObject(connectionsId);
                _banco.Usuarios.Update(usuarioDb);
                _banco.SaveChanges();

                // remover connectionId dos grupos
                var grupos = _banco.Grupos.Where(a => a.Usuarios.Contains(usuarioDb.Email));
                foreach (var connectionId in connectionsId)
                {
                    foreach (var grupo in grupos)
                    {
                        await Groups.RemoveFromGroupAsync(connectionId, grupo.Nome);
                    }

                }


            }



        }

        public async Task ObterListaUsuarios()
        {
            var usuarios = _banco.Usuarios.ToList();

            await Clients.Caller.SendAsync("ReceberListaUsuarios", usuarios);
        }

        // Signalr - Nome único
        public async Task CriarOuAbrirGrupo(string emailUserUm, string emailUserDois)
        {
            string nomeGrupo = CriarNomeGrupo(emailUserUm, emailUserDois);

            // TODO - Verificar se o grupo já existe
            var grupo = _banco.Grupos.FirstOrDefault(a => a.Nome == nomeGrupo);

            if (grupo == null)
            {

                grupo = new Grupo();
                grupo.Nome = nomeGrupo;
                grupo.Usuarios = JsonConvert.SerializeObject(new List<string>
                {
                    emailUserUm,emailUserDois
                });

                _banco.Grupos.Add(grupo);
                _banco.SaveChanges();
            }

            // TODO - Signalr

            List<string> emails = JsonConvert.DeserializeObject<List<string>>(grupo.Usuarios);
            List<Usuario> usuarios = new List<Usuario>() { _banco.Usuarios.First(a => a.Email == emails[0]), _banco.Usuarios.First(a => a.Email == emails[1]) };

            foreach (var usuario in usuarios)
            {
                var connectionsId = JsonConvert.DeserializeObject<List<string>>(usuario.ConnectionId);

                foreach (var connectionId in connectionsId)
                {
                    await Groups.AddToGroupAsync(connectionId, nomeGrupo);
                }
            }

            var mensagens = _banco.Mensagens.Where(x => x.NomeGrupo == nomeGrupo).OrderBy(x => x.DataCriacao).ToList();


            for (int i = 0; i < mensagens.Count(); i++)
            {
                mensagens[i].Usuario = JsonConvert.DeserializeObject<Usuario>(mensagens[i].UsuarioJson);
                mensagens[i].Data = ConverterData(mensagens[i].DataCriacao);
            }

            await Clients.Caller.SendAsync("AbrirGrupo", nomeGrupo, mensagens);
        }

        public async Task EnviarMensagem(Usuario usuario, string msg, string nomeGrupo)
        {
            Grupo grupo = _banco.Grupos.FirstOrDefault(a => a.Nome == nomeGrupo);
            if (!grupo.Usuarios.Contains(usuario.Email))
            {
                throw new Exception("Usuário não pertencem ao grupo");
            }

            Mensagem mensagem = new Mensagem();
            mensagem.NomeGrupo = nomeGrupo;
            mensagem.Texto = msg;
            mensagem.Usuario = usuario;
            mensagem.UsuarioId = usuario.Id.ToString();
            mensagem.UsuarioJson = JsonConvert.SerializeObject(usuario);
            mensagem.DataCriacao = DateTime.Now;
            mensagem.Data = ConverterData(DateTime.Now);

            var NomeDogrupo = grupo.Nome;

            _banco.Mensagens.Add(mensagem);
            _banco.SaveChanges();
            await Clients.Group(NomeDogrupo).SendAsync("ReceberMensagem", mensagem, NomeDogrupo);

        }



        private string CriarNomeGrupo(string user, string user1)
        {
            List<string> lista = new List<string>() { user, user1 };
            var listaOrdenada = lista.OrderBy(l => l).ToList();
            StringBuilder sb = new StringBuilder();

            foreach (var item in listaOrdenada)
            {
                sb.Append(item);
            }

            return sb.ToString();
        }
        private string ConverterData(DateTime data)
        {
            string dataConvertida = "";

            if(DataConvertida(data) == DataConvertida(DateTime.Now))
            {
                dataConvertida = "Hoje, " + data.ToString("HH:mm");
            }
            else if(DataConvertida(data) == DataConvertida(DateTime.Now.AddDays(-1)))
            {
                dataConvertida = "Ontem, " + data.ToString("HH:mm");
            }
            else
            {
                dataConvertida = data.ToString("d-m-yyyy") +", " + data.ToString("HH:mm");
            }

            return dataConvertida;
        }
        private string DataConvertida(DateTime date)
        {
            return (date.Day + "/" + date.Month + '/' + date.Year).ToString();
        }
    }
}
