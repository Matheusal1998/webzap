/* CONEXAO E RECONEXAO COM O SIGNALR  - HUB */

var connection = new signalR.HubConnectionBuilder().withUrl("/ZapWebHub").build();
var nomegrupo = "";

function ConnectionStart() {
    connection.start().then(function () {
        console.info("Connected!");
        HabilitarCadastro();
        HabilitarLogin();
        HabilitarConversacao();
    }).catch(function (err) {

        if (connection.state == 0) {
            console.error(err.toString());
            setTimeout(ConnectionStart(), 5000);
        }

    });

}

connection.onclose(async () => { await ConnectionStart(); });

/*Verificar se tela é de cadastro*/


function HabilitarCadastro() {

    var formCadastro = document.getElementById("cadastro");

    if (formCadastro != null) {
        var btnCadastro = document.getElementById("btn_cadastrar");

        btnCadastro.addEventListener("click", function () {
            var nome = document.getElementById("nome").value;
            var email = document.getElementById("email").value;
            var senha = document.getElementById("senha").value;

            var usuario = { Nome: nome, Email: email, Senha: senha };

            connection.invoke("Cadastrar", usuario);
        });
    }

    connection.on("ReceberCadastro", function (sucesso, usuario, msg) {
        var mensagem = document.getElementById("mensagem_cadastro");
        if (sucesso) {
            console.info(usuario)
        }
        mensagem.innerText = msg;
        window.location.href = "/Home/Login";
    });


}

function HabilitarLogin() {
    var formLogin = document.getElementById("form_login");



    if (formLogin != null) {


        if (GetUsuarioLogado() != null) {
            window.location.href = "/Home/Conversacao";
        }
        var btnLogin = document.getElementById("btn_acessar");
        btnLogin.addEventListener("click", function () {

            var email = document.getElementById("email").value;
            var senha = document.getElementById("senha").value;

            var usuario = { Email: email, Senha: senha };

            connection.invoke("Login", usuario);
        });
    }

    connection.on("ReceberLogin", function (sucesso, usuario, msg) {

        if (sucesso) {
            SetUsuarioLogado(usuario);
            console.info(usuario);
            window.location.href = "/Home/Conversacao";
        }
        else {
            var mensagem = document.getElementById("mensagem");
            mensagem.innerText = msg;
        }

    });
}



var telaConversacao = document.getElementById("tela-conversacao");

if (telaConversacao != null) {

    if (GetUsuarioLogado() == null) {
        window.location.href = "/Home/Login";
    }



}

function HabilitarConversacao() {
    var telaConversacao = document.getElementById("tela-conversacao");

    if (telaConversacao != null) {
        MonitorarConnectionId();
        MonitorarListaUsuarios();
        EnviarReceberMensagem();
        AbrirGrupo();
    }

}

function AbrirGrupo() {
    debugger;
    connection.on("AbrirGrupo", function (nomeGrupo, mensagens) {

        nomegrupo = nomeGrupo;
        console.log(mensagens);
        var container = document.querySelector(".container-messages");
        document.getElementById("btn_conversar").style.display = "flex";


        container.innerHTML = "";

        var mensagemHtml = "";
        for (i = 0; i < mensagens.length; i++) {
            mensagemHtml += '<div class="message message-' + (mensagens[i].usuarioId == GetUsuarioLogado().id ? "right" : "left") + '"><div class="message-head"> <img src="../imagem/chat.png"/> </div><div class="message-message">' + mensagens[i].texto + '</div> <div class="message-horario" style="' + (mensagens[i].usuario.id == GetUsuarioLogado().id ? "float:left;" : "float:rigth;") + '">' + mensagens[i].data + '</div> </div >'
        }

        container.innerHTML += mensagemHtml;


        var objDiv = document.getElementById("scroll");
        objDiv.scrollTop = objDiv.scrollHeight;
    });
}
function EnviarReceberMensagem() {
    var btnEnviar = document.getElementById("btnEnviar");

    btnEnviar.addEventListener("click", function () {
        var mensagem = document.getElementById("mensagem").value;
        if (mensagem != "") {
            connection.invoke("EnviarMensagem", GetUsuarioLogado(), mensagem, nomegrupo);
        }
    });
    document.addEventListener('keydown', function (event) {
        if (event.keyCode !== 13) return;
        var mensagem = document.getElementById("mensagem").value;

        if (mensagem != "") {
            connection.invoke("EnviarMensagem", GetUsuarioLogado(), mensagem, nomegrupo);
        }
    });

    connection.on("ReceberMensagem", function (mensagem, nomeDoGrupo) {

        document.getElementById("mensagem").value = "";
        console.log(mensagem);
        debugger;
        if (nomegrupo == nomeDoGrupo) {
            var container = document.querySelector(".container-messages");
            var mensagemHtml = '<div class="message message-' + (mensagem.usuario.id == GetUsuarioLogado().id ? "right" : "left") + '"><div class="message-head"> <img src="../imagem/chat.png"/></div><div class="message-message">' + mensagem.texto + '</div><div class="message-horario" style="' + (mensagem.usuario.id == GetUsuarioLogado().id ? "float:left;" : "float:rigth;") + '">' + mensagem.data + ' </div> </div >'
            container.innerHTML += mensagemHtml;
        }

        var objDiv = document.getElementById("scroll");
        objDiv.scrollTop = objDiv.scrollHeight;

    });
}
function MonitorarConnectionId() {

    connection.invoke("AddConnectionIdDoUsuario", GetUsuarioLogado());

    var btnSair = document.getElementById("btnSair");

    btnSair.addEventListener("click", function () {
        connection.invoke("Logout", GetUsuarioLogado()).then(function () {
            DelUsuarioLogado();
            window.location.href = "/Home/Login";
        });
    });

}
function MonitorarListaUsuarios() {

    connection.invoke("ObterListaUsuarios");
    connection.on("ReceberListaUsuarios", function (usuario) {

        var html = "";
        for (i = 0; i < usuario.length; i++) {

            if (usuario[i].id != GetUsuarioLogado().id) {
                html += '<div class="container-user-item"> <img src = "../imagem/logo.png" style = "width: 20%;"/> <div> <span>' + usuario[i].nome.split(" ", 1) + ' (' + (usuario[i].isOnline ? "online" : "offline") + ')</span></br><span class="email">' + usuario[i].email + '</span></div></div>'
            }

        }

        console.log(html);
        document.getElementById("users").innerHTML = html;

        var container = document.getElementById("users").querySelectorAll(".container-user-item");
        for (i = 0; i < container.length; i++) {
            container[i].addEventListener('click', function (event) {
                var componente = event.target || event.srcElement;
                var emailUserUm = GetUsuarioLogado().email;
                var emailUserDois = componente.parentElement.querySelector('.email').innerText;

                connection.invoke("CriarOuAbrirGrupo", emailUserUm, emailUserDois);
            });
        }
    });
}
function GetUsuarioLogado() {
    return JSON.parse(sessionStorage.getItem("Logado"));
}
function SetUsuarioLogado(usuario) {
    sessionStorage.setItem("Logado", JSON.stringify(usuario));
}
function DelUsuarioLogado() {
    sessionStorage.removeItem("Logado");
}
function OfflineDetect() {
    window.addEventListener("beeforeunload", function () {
        connection.invoke("DeleteConnectionIdDoUsuario", GetUsuarioLogado());
    });

}
ConnectionStart();